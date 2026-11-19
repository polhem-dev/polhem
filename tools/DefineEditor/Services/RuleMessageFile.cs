namespace Polhem.DefineEditor.Services;

/// <summary>A Language file a form's rule messages are translated in.</summary>
/// <param name="Language">The file's language, such as <c>zh-TW</c>.</param>
/// <param name="Path">The file.</param>
public sealed record RuleMessageFile(string Language, string Path);
