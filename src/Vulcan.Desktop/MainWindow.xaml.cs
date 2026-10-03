using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
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

            Application.Current.Shutdown();
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

    private bool IsAppAddress(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var target)
        && Uri.Compare(target, _uiAddress, UriComponents.SchemeAndServer, UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;
}
