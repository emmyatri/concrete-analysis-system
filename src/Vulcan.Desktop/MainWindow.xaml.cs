using System.IO;
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

        switch (command)
        {
            case "print-report":
                await PrintReportAsync();
                break;

            case "save-report-pdf":
                if (!root.TryGetProperty("batchNumber", out var batchElement) ||
                    batchElement.ValueKind != JsonValueKind.String)
                {
                    Log.Warning("PDF export requested without a batch number");
                    return;
                }

                await SaveReportPdfAsync(batchElement.GetString() ?? "");
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

private async Task PrintReportAsync()
{
    // Opens the WebView2 print dialog rather than printing silently.
    Browser.CoreWebView2.ShowPrintUI(
        CoreWebView2PrintDialogKind.Browser);

    Log.Information("Opened print dialog for batch report");

    await Task.CompletedTask;
}

private async Task SaveReportPdfAsync(string batchNumber)
{
    var documents = Environment.GetFolderPath(
        Environment.SpecialFolder.MyDocuments);

    var defaultName =
        $"{batchNumber} report {DateTime.Now:yyyy-MM-dd}.pdf";

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
        Log.Information("PDF export cancelled for {BatchNumber}", batchNumber);
        return;
    }

    try
    {
        var saved = await Browser.CoreWebView2.PrintToPdfAsync(
            dialog.FileName);

        if (!saved)
        {
            throw new IOException("WebView2 reported PDF export failure.");
        }

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
}

    private bool IsAppAddress(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var target)
        && Uri.Compare(target, _uiAddress, UriComponents.SchemeAndServer, UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;
}
