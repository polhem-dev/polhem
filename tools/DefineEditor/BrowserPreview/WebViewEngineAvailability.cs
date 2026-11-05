using Avalonia.Platform;

namespace Polhem.DefineEditor.BrowserPreview;

/// <summary>
/// Whether this machine has the web engine the web preview inside the editor needs: WebView2 on Windows, WKWebView on
/// macOS, and WPE WebKit or WebKitGTK on Linux.
/// </summary>
/// <remarks>
/// WARNING: the check has to come before a <c>NativeWebView</c> is created. Avalonia's WebView picks WebView2 on Windows
/// whether or not its runtime is installed, and the failure only surfaces later, inside the control.
/// </remarks>
public static class WebViewEngineAvailability
{
    /// <summary>
    /// Returns why the web preview cannot run here, or <c>null</c> when the engine is installed.
    /// </summary>
    public static string? GetUnavailableReason()
    {
        var candidates = OperatingSystem.IsWindows() ? [WebViewAdapterType.WebView2]
            : OperatingSystem.IsMacOS() ? [WebViewAdapterType.WkWebView]
            : OperatingSystem.IsLinux() ? new[] { WebViewAdapterType.WpeWebKit, WebViewAdapterType.WebKitGtk }
            : [];

        string? reason = null;
        foreach (var candidate in candidates)
        {
            var info = WebViewAdapterInfo.GetAdapterInfo(candidate);
            if (info.IsSupported && info.IsInstalled)
                return null;
            reason ??= info.UnavailableReason;
        }
        return reason ?? "No web engine is available on this platform.";
    }
}
