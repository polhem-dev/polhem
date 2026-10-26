using Polhem.Definition.Settings;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// A form the relation dialog offers to relate a field to: a program of the solution's ProgramSettings, or the field's
/// current related form when ProgramSettings does not register it.
/// </summary>
/// <param name="ProgId">The program id.</param>
/// <param name="DisplayName">The program's display name from ProgramSettings; empty when it has none.</param>
/// <param name="HasFormSchema">Whether the solution has a FormSchema file for the program, which listing its fields needs.</param>
/// <param name="IsRegistered">Whether ProgramSettings registers the program.</param>
public sealed record RelationProgramChoice(string ProgId, string DisplayName, bool HasFormSchema, bool IsRegistered)
{
    /// <summary>Gets whether the program can be picked: only a form with a FormSchema has fields to map.</summary>
    public bool IsSelectable => HasFormSchema;

    /// <summary>Gets the text the picker matches and shows for the program.</summary>
    public string Label => string.IsNullOrEmpty(DisplayName) ? ProgId : $"{ProgId} - {DisplayName}";

    /// <inheritdoc/>
    public override string ToString() => ProgId;

    /// <summary>
    /// The programs to offer, in the order <paramref name="settings"/> declares them. Nothing but ProgramSettings is read:
    /// whether a program has a FormSchema comes from <paramref name="formSchemaProgIds"/>, the files the solution tree
    /// already found.
    /// </summary>
    /// <param name="settings">The solution's ProgramSettings, or <c>null</c> when it is missing or cannot be read, in which
    /// case every form of <paramref name="formSchemaProgIds"/> is offered without a name.</param>
    /// <param name="formSchemaProgIds">The program ids that have a FormSchema file.</param>
    /// <param name="currentProgId">The field's current related form, put first when ProgramSettings does not register it
    /// so the dialog can show what the field points to.</param>
    public static IReadOnlyList<RelationProgramChoice> Build(ProgramSettings? settings,
        IReadOnlyCollection<string> formSchemaProgIds, string? currentProgId)
    {
        ArgumentNullException.ThrowIfNull(formSchemaProgIds);
        var hasSchema = new HashSet<string>(formSchemaProgIds, StringComparer.OrdinalIgnoreCase);
        var choices = new List<RelationProgramChoice>();
        if (settings?.Items is { } items)
        {
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.ProgId)
                    || choices.Exists(c => string.Equals(c.ProgId, item.ProgId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                choices.Add(new RelationProgramChoice(item.ProgId, item.DisplayName,
                    hasSchema.Contains(item.ProgId), IsRegistered: true));
            }
        }
        else
        {
            choices.AddRange(formSchemaProgIds.Select(p => new RelationProgramChoice(p, string.Empty, true, IsRegistered: false)));
        }

        if (!string.IsNullOrWhiteSpace(currentProgId)
            && !choices.Exists(c => string.Equals(c.ProgId, currentProgId, StringComparison.OrdinalIgnoreCase)))
        {
            choices.Insert(0, new RelationProgramChoice(currentProgId, string.Empty, hasSchema.Contains(currentProgId),
                IsRegistered: false));
        }
        return choices;
    }

    /// <summary>
    /// Returns whether <paramref name="keyword"/> occurs in the program id or the display name, ignoring case; an empty
    /// keyword matches every program.
    /// </summary>
    public bool Matches(string? keyword) =>
        string.IsNullOrWhiteSpace(keyword)
        || ProgId.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase)
        || DisplayName.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The choices that match <paramref name="keyword"/>: those whose program id starts with it first, then the rest, each
    /// group in the order given.
    /// </summary>
    public static IReadOnlyList<RelationProgramChoice> Filter(IEnumerable<RelationProgramChoice> choices, string? keyword)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var matches = choices.Where(c => c.Matches(keyword)).ToList();
        if (string.IsNullOrWhiteSpace(keyword)) return matches;
        var key = keyword.Trim();
        return matches.Where(c => c.ProgId.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            .Concat(matches.Where(c => !c.ProgId.StartsWith(key, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
