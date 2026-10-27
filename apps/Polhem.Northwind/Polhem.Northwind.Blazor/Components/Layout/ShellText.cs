using System.Globalization;

namespace Polhem.Northwind.Blazor.Components.Layout;

/// <summary>
/// The few strings of this head's own shell, in English and Traditional Chinese. The form pages take their text
/// from the framework.
/// </summary>
public static class ShellText
{
    private static readonly Dictionary<string, (string En, string ZhTw)> s_text = new(StringComparer.Ordinal)
    {
        ["Company"] = ("Company", "公司"),
        ["SignOut"] = ("Sign out", "登出"),
        ["Menu"] = ("Menu", "選單"),
        ["Hint"] = ("Use demo / demo. The language below applies to this browser; pick it before signing in.",
            "請使用 demo / demo。下方的語言套用於這個瀏覽器，請在登入前選擇。"),
        ["EnterCompanyFailed"] = ("Signed in, but entering the company failed: {0}", "已登入，但進入公司失敗：{0}"),
        ["Loading"] = ("Loading…", "載入中…"),
    };

    /// <summary>Gets the text of <paramref name="key"/> in the current UI culture.</summary>
    /// <param name="key">The text key.</param>
    public static string Get(string key)
    {
        var (en, zhTw) = s_text[key];
        return CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? zhTw : en;
    }
}
