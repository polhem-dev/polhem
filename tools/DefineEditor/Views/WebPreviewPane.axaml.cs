using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Styling;
using Polhem.DefineEditor.BrowserPreview;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// The web preview inside the FormLayout editor: the browser preview page, the one the globe button opens in the
/// default browser, shown in a <see cref="NativeWebView"/> drawn by the system's web engine.
/// </summary>
/// <remarks>
/// The page follows the document through the browser preview server, as any browser showing it does, so this pane only
/// opens it, and opens it again when the document switches to the other Blazor package. It creates the web view the
/// first time it is shown, and only when <see cref="WebViewEngineAvailability"/> finds the engine; otherwise it explains
/// why and offers the browser instead.
/// </remarks>
public partial class WebPreviewPane : UserControl
{
    private NativeWebView? _webView;
    private Uri? _pageUrl;
    private BrowserPreviewUi _pageUi;
    private bool _loading;

    public WebPreviewPane()
    {
        InitializeComponent();
        // The page cannot see the editor's theme, so it is loaded again with the canvas of the new one.
        ActualThemeVariantChanged += (_, _) =>
        {
            if (_webView is not null && _pageUrl is not null)
                _webView.Source = WithCanvas(_pageUrl);
        };
    }

    /// <summary>
    /// Opens the document's preview page in the package it asks for, unless that page is open or opening already.
    /// </summary>
    public async void EnsureLoaded(FormLayoutDocumentViewModel document)
    {
        if (_loading || (_webView is not null && _pageUi == document.PreviewWebUi)) return;

        if (WebViewEngineAvailability.GetUnavailableReason() is { } reason)
        {
            ShowMessage(string.Format(LocalizationService.Current.Culture, LocalizationService.Current["Preview_WebUnavailable"], reason));
            return;
        }

        _loading = true;
        try
        {
            var ui = document.PreviewWebUi;
            _pageUrl = await document.PublishBrowserPreviewAsync(BrowserPreviewHost.Shared);
            _pageUi = ui;
            if (_webView is null)
            {
                _webView = CreateWebView();
                WebViewHost.Children.Add(_webView);
            }
            _webView.Source = WithCanvas(_pageUrl);
        }
        catch (InvalidOperationException ex)
        {
            ShowStartFailure(ex);
        }
        catch (IOException ex)
        {
            ShowStartFailure(ex);
        }
        finally
        {
            _loading = false;
        }
        // The document may have switched packages while its page was being published.
        if (_webView is not null && document.IsWebPreview && _pageUi != document.PreviewWebUi)
            EnsureLoaded(document);
    }

    // The canvas around the form follows the editor's theme; the form itself keeps the application's light theme.
    // The URL already carries the package in its query, so the canvas is added to it.
    private Uri WithCanvas(Uri pageUrl)
    {
        var canvas = ActualThemeVariant == ThemeVariant.Dark ? "canvas=dark" : "canvas=light";
        var query = pageUrl.Query.TrimStart('?');
        return new UriBuilder(pageUrl) { Query = query.Length == 0 ? canvas : query + "&" + canvas }.Uri;
    }

    private NativeWebView CreateWebView()
    {
        var webView = new NativeWebView();
        webView.EnvironmentRequested += OnEnvironmentRequested;
        webView.NavigationCompleted += OnNavigationCompleted;
        return webView;
    }

    // The page is regenerated from the editor's state on every run, so nothing it stores is worth keeping: the engines
    // keep their data in memory or in a temporary folder rather than in the user's profile.
    private static void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
#if DEBUG
        e.EnableDevTools = true;
#endif
        switch (e)
        {
            case WindowsWebView2EnvironmentRequestedEventArgs webView2:
                webView2.IsInPrivateModeEnabled = true;
                // NOTE: WebView2 otherwise creates its data folder next to the executable, which may not be writable.
                webView2.UserDataFolder = Path.Combine(Path.GetTempPath(), "Polhem.DefineEditor.WebView2");
                break;
            case AppleWKWebViewEnvironmentRequestedEventArgs apple:
                apple.NonPersistentDataStore = true;
                break;
            case GtkWebViewEnvironmentRequestedEventArgs gtk:
                gtk.EphemeralDataManager = true;
                break;
            case LinuxWpeWebViewEnvironmentRequestedEventArgs wpe:
                wpe.DataDirectory = Path.Combine(Path.GetTempPath(), "Polhem.DefineEditor.WebKit", "data");
                wpe.CacheDirectory = Path.Combine(Path.GetTempPath(), "Polhem.DefineEditor.WebKit", "cache");
                break;
        }
    }

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            MessagePanel.IsVisible = false;
            WebViewHost.IsVisible = true;
            return;
        }
        ShowMessage(LocalizationService.Current["Preview_WebLoadFailed"]);
    }

    private void ShowStartFailure(Exception ex)
        => ShowMessage(string.Format(LocalizationService.Current.Culture, LocalizationService.Current["Status_BrowserPreviewFailed"], ex.Message));

    private void ShowMessage(string message)
    {
        // A native web view draws over everything in its area, so it is hidden rather than covered.
        WebViewHost.IsVisible = false;
        MessageText.Text = message;
        MessagePanel.IsVisible = true;
    }
}
