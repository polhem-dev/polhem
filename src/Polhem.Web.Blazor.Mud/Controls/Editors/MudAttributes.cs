namespace Polhem.Web.Blazor.Mud.Controls.Editors
{
    /// <summary>
    /// Adapts the parameters of the field editor bases to what MudBlazor's inputs take.
    /// </summary>
    internal static class MudAttributes
    {
        // MudBlazor's own default, which means no practical limit.
        private const int NoLimit = 524288;

        internal static Dictionary<string, object?> From(IReadOnlyDictionary<string, object>? attributes)
            => attributes?.ToDictionary(a => a.Key, a => (object?)a.Value, StringComparer.Ordinal) ?? [];

        internal static int MaxLength(int maxLength) => maxLength > 0 ? maxLength : NoLimit;
    }
}
