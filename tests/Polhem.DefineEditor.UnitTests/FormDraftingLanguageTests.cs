using System.ComponentModel;
using System.Globalization;
using Polhem.AI.Claude;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;
using Polhem.Definition.Drafting;
using Polhem.Definition.Drafting.UnitTests;

namespace Polhem.DefineEditor.UnitTests;

/// <summary>
/// The drafting window's texts that the view model builds, rather than the view taking them from a resource, follow a
/// change of the interface language while the window is open.
/// </summary>
[Collection(nameof(InterfaceLanguageCollection))]
public sealed class FormDraftingLanguageTests
{
    private static string Describe() =>
        DraftingChatClients.DescribeCredential(ClaudeCredentialKind.Profile, "default", key => LocalizationService.Current[key]);

    private static FormDraftingViewModel CreateViewModel()
    {
        var host = new FakeDraftingHost();
        return new FormDraftingViewModel(new FormDraftSession(new ScriptedChatClient(), host), host, Path.GetTempPath(),
            "leave.pdf", Describe, _ => Task.FromResult(false));
    }

    [Fact]
    [DisplayName("Changing the interface language rewrites the credential line and the Questions tab title of an open window")]
    public void CultureChanged_OpenWindow_RewritesCredentialAndIssuesTab()
    {
        var language = LocalizationService.Current;
        var original = language.Culture;
        try
        {
            language.Culture = CultureInfo.GetCultureInfo("en");
            using var viewModel = CreateViewModel();
            Assert.Equal("Signed in with ant auth login (profile: default)", viewModel.CredentialText);
            Assert.Equal("Questions", viewModel.IssuesTabTitle);
            var changed = new List<string?>();
            viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            language.Culture = CultureInfo.GetCultureInfo("zh-TW");

            Assert.Equal("已用 ant auth login 登入（設定檔：default）", viewModel.CredentialText);
            Assert.Equal("問題", viewModel.IssuesTabTitle);
            Assert.Contains(nameof(FormDraftingViewModel.CredentialText), changed);
            Assert.Contains(nameof(FormDraftingViewModel.IssuesTabTitle), changed);
        }
        finally
        {
            language.Culture = original;
        }
    }

    [Fact]
    [DisplayName("A closed window no longer listens to language changes")]
    public void CultureChanged_AfterDispose_RaisesNothing()
    {
        var language = LocalizationService.Current;
        var original = language.Culture;
        try
        {
            language.Culture = CultureInfo.GetCultureInfo("en");
            var viewModel = CreateViewModel();
            var changed = new List<string?>();
            viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            viewModel.Dispose();

            language.Culture = CultureInfo.GetCultureInfo("zh-TW");

            Assert.Empty(changed);
        }
        finally
        {
            language.Culture = original;
        }
    }
}
