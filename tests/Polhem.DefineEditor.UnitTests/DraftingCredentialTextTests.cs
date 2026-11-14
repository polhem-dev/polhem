using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Polhem.AI.Claude;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The credential line of the drafting dialogs is written in the interface language, not in the English the provider
    /// package uses.
    /// </summary>
    public class DraftingCredentialTextTests
    {
        private static readonly ResourceManager s_resources =
            new("Polhem.DefineEditor.Resources.Strings", typeof(LocalizationService).Assembly);

        private static Func<string, string> Lookup(string culture)
        {
            var info = CultureInfo.GetCultureInfo(culture);
            return key => s_resources.GetString(key, info) ?? key;
        }

        [Fact]
        [DisplayName("A signed-in profile is described in Traditional Chinese with its profile name")]
        public void DescribeCredential_ProfileInZhTw_IsChineseWithProfileName()
        {
            var text = DraftingChatClients.DescribeCredential(ClaudeCredentialKind.Profile, "default", Lookup("zh-TW"));

            Assert.Equal("已用 ant auth login 登入（設定檔：default）", text);
        }

        [Fact]
        [DisplayName("A signed-in profile is described in English with its profile name")]
        public void DescribeCredential_ProfileInEnglish_HasProfileName()
        {
            var text = DraftingChatClients.DescribeCredential(ClaudeCredentialKind.Profile, "work", Lookup("en"));

            Assert.Equal("Signed in with ant auth login (profile: work)", text);
        }

        [Theory]
        [InlineData(ClaudeCredentialKind.None)]
        [InlineData(ClaudeCredentialKind.ApiKey)]
        [InlineData(ClaudeCredentialKind.AuthToken)]
        [InlineData(ClaudeCredentialKind.Profile)]
        [InlineData(ClaudeCredentialKind.WorkloadIdentity)]
        [InlineData(ClaudeCredentialKind.FederatedProfile)]
        [DisplayName("Every credential kind has its own English and Traditional Chinese text")]
        public void DescribeCredential_EveryKind_HasEnglishAndChineseText(ClaudeCredentialKind kind)
        {
            var english = DraftingChatClients.DescribeCredential(kind, "p", Lookup("en"));
            var chinese = DraftingChatClients.DescribeCredential(kind, "p", Lookup("zh-TW"));

            Assert.DoesNotContain("Drafting_", english, StringComparison.Ordinal);
            Assert.DoesNotContain("Drafting_", chinese, StringComparison.Ordinal);
            Assert.NotEqual(english, chinese);
        }

        [Fact]
        [DisplayName("Every credential kind is covered by the theory above")]
        public void DescribeCredential_TheoryCoversEveryKind()
        {
            Assert.Equal(6, Enum.GetValues<ClaudeCredentialKind>().Length);
        }
    }
}
