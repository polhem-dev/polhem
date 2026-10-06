namespace Polhem.DefineEditor.Services;

/// <summary>
/// Translates the fixed labels of the editor trees (folder and root names from the
/// <c>[TreeNode]</c> annotations, and the groups the editors add) through the
/// <c>TreeNode_*</c> string resources. A label without a resource, such as a composite
/// format like <c>{0} - {1}</c>, is shown as written.
/// </summary>
public static class TreeLabels
{
    public static string Translate(string label)
    {
        var key = "TreeNode_" + label.Replace(" ", string.Empty, StringComparison.Ordinal);
        var text = LocalizationService.Current[key];
        return string.Equals(text, key, StringComparison.Ordinal) ? label : text;
    }
}
