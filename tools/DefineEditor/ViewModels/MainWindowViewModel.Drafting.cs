using System.Globalization;
using Microsoft.Extensions.AI;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.Views;
using Polhem.Definition.Drafting;
using Polhem.Definition.Drafting.Documents;
using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>The drafting window currently open, so a second request activates it instead of opening another.</summary>
    private FormDraftingWindow? _draftingWindow;

    /// <summary>
    /// Creates the chat client a drafting conversation talks to. <see langword="null"/>, the default, uses the provider
    /// in <see cref="UserSettings.DraftingProvider"/>; the smoke run replaces it with a scripted client.
    /// </summary>
    public Func<IChatClient>? DraftingChatClientFactory { get; set; }

    /// <summary>
    /// Describes the credential the drafting provider will use. <see langword="null"/>, the default, asks the provider
    /// in <see cref="UserSettings.DraftingProvider"/>.
    /// </summary>
    public Func<DraftingCredential>? DraftingCredentialProvider { get; set; }

    /// <summary>
    /// Drafts a form from documents in a conversation with a model, then imports it the way a workbook is imported.
    /// Nothing leaves the machine before the user agreed to send the documents.
    /// </summary>
    /// <param name="documentPaths">The documents: PDF, Word or images, such as the front and back of a paper form.</param>
    public async Task ImportFormDocumentsAsync(IReadOnlyList<string> documentPaths)
    {
        ArgumentNullException.ThrowIfNull(documentPaths);
        if (!IsSolutionOpened || documentPaths.Count == 0) return;
        if (_draftingWindow is not null)
        {
            _draftingWindow.Activate();
            return;
        }
        var owner = GetOwnerWindow();
        var provider = UserSettings.Load().DraftingProvider;

        var credential = DraftingCredentialProvider?.Invoke() ?? DraftingChatClients.DescribeCredential(provider);
        if (!credential.IsAvailable)
        {
            if (owner is not null)
                await ConfirmationDialog.ShowMessageAsync(owner, L("Drafting_NoCredentialTitle"), L("Drafting_NoCredentialMessage"));
            StatusText = L("Status_DraftingNoCredential");
            return;
        }

        IReadOnlyList<AIContent> documents;
        try
        {
            documents = DocumentSource.FromFiles(documentPaths);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
            or DocumentTooLargeException or InvalidDataException)
        {
            if (owner is not null)
                await ConfirmationDialog.ShowMessageAsync(owner, L("Drafting_ReadFailedTitle"), ex.Message);
            StatusText = L("Status_DraftingReadFailed", ex.Message);
            return;
        }

        if (owner is not null && !await ConfirmationDialog.ShowAsync(owner, L("Drafting_ConsentTitle"),
                DraftingConsentMessage(documentPaths, credential), confirmLabel: L("Drafting_ConsentSend")))
        {
            StatusText = L("Status_DraftingCancelled");
            return;
        }

        var host = new DefineEditorDraftHost(Solution, SolutionPath);
        var chatClient = DraftingChatClientFactory?.Invoke() ?? DraftingChatClients.Create(provider);
        var session = new FormDraftSession(chatClient, host);
        var documentsLabel = string.Join(", ", documentPaths.Select(Path.GetFileName));
        var viewModel = new FormDraftingViewModel(session, host, SolutionPath, documentsLabel, credential.Description,
            result => ImportDraftedFormAsync(result, documentsLabel));

        if (owner is null)
        {
            // Headless (the smoke run): no window to talk in, so the first turn alone decides.
            await viewModel.StartAsync(documents);
            if (viewModel.IsReady)
                await ImportDraftedFormAsync(FormDraftConverter.Convert(session.Draft), documentsLabel);
            viewModel.Dispose();
            chatClient.Dispose();
            return;
        }

        _draftingWindow = FormDraftingWindow.Open(owner, viewModel);
        _draftingWindow.Closed += (_, _) =>
        {
            _draftingWindow = null;
            viewModel.Dispose();
            chatClient.Dispose();
        };
        StatusText = L("Status_DraftingStarted", documentsLabel);
        await viewModel.StartAsync(documents);
    }

    /// <summary>Imports a drafted form through the same steps as a workbook, with the dialogs over the drafting window.</summary>
    private async Task<bool> ImportDraftedFormAsync(FormDraftResult result, string documentsLabel)
    {
        var content = FormDraftImport.ToContent(result);
        FormImportPlan plan;
        try
        {
            plan = FormImporter.Plan(content, new FormImportOptions { DefinePath = SolutionPath });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusText = L("Status_FormImportFailed", ex.Message);
            return false;
        }
        var owner = (Avalonia.Controls.Window?)_draftingWindow ?? GetOwnerWindow();
        return await ImportFormContentAsync(content, [], plan, documentsLabel, owner, onWritten: () => _draftingWindow?.Close());
    }

    private static string DraftingConsentMessage(IReadOnlyList<string> documentPaths, DraftingCredential credential)
    {
        var files = string.Join("\n", documentPaths.Select(path =>
            $"• {Path.GetFileName(path)} ({FormatSize(new FileInfo(path).Length)})"));
        var message = L("Drafting_ConsentMessage", files, credential.Description);
        if (credential.Warnings.Count > 0)
            message += "\n\n" + L("Drafting_CredentialWarnings", string.Join("\n", credential.Warnings.Select(w => "• " + w)));
        return message;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):0.#} MB"),
    };
}
