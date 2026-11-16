using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Serilog;

namespace Vulcan.Desktop;

/// <summary>
/// The only window. It hosts WebView2, which shows the Angular UI (ADR-004).
/// </summary>
public partial class MainWindow : Window
{
    // WebView2 can't write next to the program in C:\Program Files, so its data lives with ours (ADR-004 section: 2.5).
    private static readonly string _userDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VulcanCBAS",
        "WebView2");

    private readonly Uri _uiAddress;
    private bool _isPdfExportInProgress;
    private readonly HttpClient _exportClient = new();
    private bool _isDataExportInProgress;
    private bool _isCloudSaveInProgress;

    public MainWindow(Uri uiAddress)
    {
        InitializeComponent();
        _uiAddress = uiAddress;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _userDataFolder);
            await Browser.EnsureCoreWebView2Async(environment);

            ConfigureBrowser(Browser.CoreWebView2);
            Browser.CoreWebView2.Navigate(_uiAddress.ToString());

            Log.Information("WebView2 showing {UiAddress}", _uiAddress);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "WebView2 failed to start");

            MessageBox.Show(
                "The application window could not be loaded. The error has been recorded in the application log.",
                "Vulcan CBAS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            System.Windows.Application.Current.Shutdown();
        }
    }

    private void ConfigureBrowser(CoreWebView2 browser)
    {
#if !DEBUG
        // Release: behave like an app, not a browser (ADR-004 §2.5).
        // Copy, paste and undo still work with accelerator keys off.
        browser.Settings.AreDevToolsEnabled = false;
        browser.Settings.AreDefaultContextMenusEnabled = false;
        browser.Settings.AreBrowserAcceleratorKeysEnabled = false;
#endif
        browser.Settings.IsStatusBarEnabled = false;
        // SCRUM-212: Handle print and PDF requests from the Angular report page.
        browser.WebMessageReceived += OnWebMessageReceived;

        // Never leave the app's own address.
        browser.NavigationStarting += (_, args) =>
        {
            if (!IsAppAddress(args.Uri))
            {
                Log.Warning("Blocked navigation to {Uri}", args.Uri);
                args.Cancel = true;
            }
        };

        // No pop-up windows or new tabs.
        browser.NewWindowRequested += (_, args) =>
        {
            Log.Warning("Blocked new window for {Uri}", args.Uri);
            args.Handled = true;
        };
    }

    private bool TryGetCurrentReportBatchNumber(out string batchNumber)
    {
        batchNumber = string.Empty;

        var source = Browser.CoreWebView2?.Source;

        if (string.IsNullOrWhiteSpace(source) ||
            !IsAppAddress(source) ||
            !Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split(
            '/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 3 ||
            !segments[0].Equals("batches", StringComparison.OrdinalIgnoreCase) ||
            !segments[2].Equals("report", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = Uri.UnescapeDataString(segments[1]);

        // Restrict batch numbers to safe filename characters.
        if (candidate.Length is < 1 or > 100 ||
            !candidate.All(c =>
                char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return false;
        }

        batchNumber = candidate;
        return true;
    }

    private async void OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            // Only accept messages originating from our application.
            if (!IsAppAddress(e.Source))
            {
                Log.Warning("Rejected WebView2 message from {Source}", e.Source);
                return;
            }

            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String)
            {
                Log.Warning("Ignored invalid WebView2 message");
                return;
            }

            var command = typeElement.GetString();

            // SCRUM-214: the Settings page asks for the folder picker.
            if (command == "choose-cloud-folder")
            {
                if (!IsCurrentPage("settings"))
                {
                    Log.Warning("Rejected folder picker request outside Settings");
                    return;
                }

                ChooseCloudFolder();
                return;
            }

            if (!TryGetCurrentReportBatchNumber(out var reportBatchNumber))
            {
                Log.Warning("Rejected report command outside a valid batch report");
                return;
            }

            switch (command)
            {
                case "print-report":
                    await PrintReportAsync();
                    break;

                case "save-report-pdf":
                    if (!root.TryGetProperty("batchNumber", out var batchElement) ||
                        batchElement.ValueKind != JsonValueKind.String ||
                        !string.Equals(
                            batchElement.GetString(),
                            reportBatchNumber,
                            StringComparison.Ordinal))
                    {
                        Log.Warning("Rejected PDF export with invalid or mismatched batch number");
                        return;
                    }

                    await SaveReportPdfAsync(reportBatchNumber);
                    break;

                case "save-batch-data":
                    if (!root.TryGetProperty("batchNumber", out var exportBatchElement) ||
                        exportBatchElement.ValueKind != JsonValueKind.String ||
                        !string.Equals(
                            exportBatchElement.GetString(),
                            reportBatchNumber,
                            StringComparison.Ordinal))
                    {
                        Log.Warning(
                            "Rejected data export with invalid or mismatched batch number");
                        return;
                    }

                    await SaveBatchDataAsync(reportBatchNumber);
                    break;

                case "save-to-cloud":
                    if (!root.TryGetProperty("batchNumber", out var cloudBatchElement) ||
                        cloudBatchElement.ValueKind != JsonValueKind.String ||
                        !string.Equals(
                            cloudBatchElement.GetString(),
                            reportBatchNumber,
                            StringComparison.Ordinal))
                    {
                        Log.Warning(
                            "Rejected cloud save with invalid or mismatched batch number");
                        return;
                    }

                    await SaveToCloudAsync(reportBatchNumber);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to handle WebView2 report command");

            MessageBox.Show(
                "The report operation could not be completed.",
                "Vulcan CBAS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private Task PrintReportAsync()
    {
        // SCRUM-212: Use WebView2's browser print dialog instead of the
        // native Windows system dialog. The system dialog does not provide
        // a document preview, making it difficult to verify that the
        // exported PDF matches the printed report (acceptance criterion 5).
        // The browser dialog preserves print preview while still allowing
        // users to select their installed Windows printers.
        Browser.CoreWebView2.ShowPrintUI(
            CoreWebView2PrintDialogKind.Browser);

        Log.Information("Opened print dialog for batch report");

        return Task.CompletedTask;
    }

    private async Task SaveReportPdfAsync(string batchNumber)
    {
        if (_isPdfExportInProgress)
        {
            Log.Warning(
                "Ignored overlapping PDF export request for {BatchNumber}",
                batchNumber);
            return;
        }

        _isPdfExportInProgress = true;

        try
        {
            var documents = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);

            var defaultName = ReportPdfFileName(batchNumber);

            var dialog = new SaveFileDialog
            {
                Title = "Save Batch Report as PDF",
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                FileName = defaultName,
                InitialDirectory = documents,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                Log.Information(
                    "PDF export cancelled for {BatchNumber}",
                    batchNumber);
                return;
            }

            await WriteReportPdfAsync(dialog.FileName);

            Log.Information(
                "Saved batch report PDF for {BatchNumber} to {Path}",
                batchNumber,
                dialog.FileName);
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "Failed to save batch report PDF for {BatchNumber}",
                batchNumber);

            MessageBox.Show(
                "Couldn't save the PDF. Check the folder isn't read-only.",
                "Vulcan CBAS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isPdfExportInProgress = false;
        }
    }

    private async Task SaveBatchDataAsync(string batchNumber)
    {
        if (_isDataExportInProgress)
        {
            Log.Warning(
                "Ignored overlapping data export request for {BatchNumber}",
                batchNumber);
            return;
        }

        _isDataExportInProgress = true;

        try
        {
            var documents = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);

            var dialog = new SaveFileDialog
            {
                Title = "Export Batch Data",
                Filter = "ZIP archives (*.zip)|*.zip",
                DefaultExt = ".zip",
                AddExtension = true,
                FileName = ExportZipFileName(batchNumber),
                InitialDirectory = documents,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                Log.Information(
                    "Data export cancelled for {BatchNumber}",
                    batchNumber);
                return;
            }

            await DownloadExportAsync(batchNumber, dialog.FileName);

            Log.Information(
                "Exported batch data for {BatchNumber} to {Path}",
                batchNumber,
                dialog.FileName);
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "Failed to export batch data for {BatchNumber}",
                batchNumber);

            MessageBox.Show(
                "Couldn't export the batch data. Check the selected folder and try again.",
                "Vulcan CBAS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isDataExportInProgress = false;
        }
    }

    /// <summary>
    /// SCRUM-214: writes the report PDF and the data export to &lt;cloud folder&gt;\CBAS\&lt;batch number&gt;
    /// (ADR-007). The sync app (OneDrive, Google Drive) uploads them; CBAS never uploads anything itself,
    /// and only writes when the operator clicks Save to cloud (SC-6).
    /// </summary>
    private async Task SaveToCloudAsync(string batchNumber)
    {
        if (_isCloudSaveInProgress)
        {
            Log.Warning(
                "Ignored overlapping cloud save request for {BatchNumber}",
                batchNumber);
            return;
        }

        _isCloudSaveInProgress = true;

        try
        {
            var cloudFolder = await GetCloudFolderAsync();

            if (cloudFolder is null)
            {
                MessageBox.Show(
                    "Choose a cloud folder in Settings first.",
                    "Vulcan CBAS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!Directory.Exists(cloudFolder))
            {
                Log.Warning("Cloud folder {CloudFolder} not found", cloudFolder);

                MessageBox.Show(
                    $"Can't find the cloud folder:\n{cloudFolder}\n\n" +
                    "Check that it still exists and that your sync app (OneDrive, Google Drive) is running, " +
                    "or choose another folder in Settings.",
                    "Vulcan CBAS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var destination = Directory.CreateDirectory(
                Path.Combine(cloudFolder, "CBAS", batchNumber)).FullName;

            await WriteReportPdfAsync(Path.Combine(destination, ReportPdfFileName(batchNumber)));
            await DownloadExportAsync(batchNumber, Path.Combine(destination, ExportZipFileName(batchNumber)));

            Log.Information(
                "Saved report and data for {BatchNumber} to cloud folder {Path}",
                batchNumber,
                destination);

            MessageBox.Show(
                $"Saved the report and data for {batchNumber} to:\n{destination}\n\n" +
                "Your sync app will upload them.",
                "Vulcan CBAS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "Failed to save {BatchNumber} to the cloud folder",
                batchNumber);

            MessageBox.Show(
                "Couldn't save to the cloud folder. Check that the folder isn't read-only and try again.",
                "Vulcan CBAS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isCloudSaveInProgress = false;
        }
    }

    /// <summary>SCRUM-214: the folder picker for Settings. The page saves the choice through the API.</summary>
    private void ChooseCloudFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose the cloud folder",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            Log.Information("Cloud folder choice cancelled");
            return;
        }

        Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "cloud-folder-chosen",
            path = dialog.FolderName
        }));
    }

    /// <summary>The cloud folder saved in Settings, or null when none is set.</summary>
    private async Task<string?> GetCloudFolderAsync()
    {
        using var response = await _exportClient.GetAsync(
            new Uri(_uiAddress, "/api/settings/cloud-folder"));

        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement.TryGetProperty("path", out var path) &&
               path.ValueKind == JsonValueKind.String
            ? path.GetString()
            : null;
    }

    /// <summary>
    /// Prints the current report to a PDF at <paramref name="path"/>. Writes a temporary file first so a
    /// failed export never leaves a partial PDF for a sync app to upload.
    /// </summary>
    private async Task WriteReportPdfAsync(string path)
    {
        // ADR-007: Preserve report background colors and warning indicators
        // when exporting to PDF. Disable WebView2's default headers and
        // footers so the report's own print styling controls the layout.
        var settings = Browser.CoreWebView2.Environment.CreatePrintSettings();

        settings.ShouldPrintBackgrounds = true;
        settings.ShouldPrintHeaderAndFooter = false;
        settings.Orientation = CoreWebView2PrintOrientation.Portrait;
        settings.PageWidth = 8.5;
        settings.PageHeight = 11;

        var temporaryPath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.pdf");

        try
        {
            var saved = await Browser.CoreWebView2.PrintToPdfAsync(
                temporaryPath,
                settings);

            if (!saved)
            {
                throw new IOException(
                    "WebView2 reported PDF export failure.");
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>Downloads the batch's export zip from the local API to <paramref name="path"/>.</summary>
    private async Task DownloadExportAsync(string batchNumber, string path)
    {
        // Retrieve the archive from the local in-process API.
        var requestUri = new Uri(
            _uiAddress,
            $"/api/batches/{Uri.EscapeDataString(batchNumber)}/export");

        using var response = await _exportClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentType?.MediaType
            != "application/zip")
        {
            throw new InvalidDataException(
                "The export endpoint did not return a ZIP archive.");
        }

        // Download into a temporary file first so an interrupted
        // request does not leave a partially written export.
        var temporaryPath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.zip");

        try
        {
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                await response.Content.CopyToAsync(destination);
            }

            // Do not overwrite the chosen file until the
            // complete download has succeeded.
            File.Move(
                temporaryPath,
                path,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string ReportPdfFileName(string batchNumber) =>
        $"{batchNumber} report {DateTime.Now:yyyy-MM-dd}.pdf";

    private static string ExportZipFileName(string batchNumber) =>
        $"{batchNumber}-export.zip";

    private bool IsCurrentPage(string page)
    {
        var source = Browser.CoreWebView2?.Source;

        return !string.IsNullOrWhiteSpace(source) &&
               IsAppAddress(source) &&
               Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
               uri.AbsolutePath.Trim('/').Equals(page, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAppAddress(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var target)
        && Uri.Compare(target, _uiAddress, UriComponents.SchemeAndServer, UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;
}
