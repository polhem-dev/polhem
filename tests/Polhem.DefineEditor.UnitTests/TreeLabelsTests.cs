using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using Polhem.Definition.Attributes;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The tree label translation: what <see cref="TreeLabels"/> returns, and that every fixed
    /// <c>[TreeNode]</c> label has its text in both languages.
    /// </summary>
    public class TreeLabelsTests
    {
        private static readonly ResourceManager s_resources =
            new("Polhem.DefineEditor.Resources.Strings", typeof(LocalizationService).Assembly);

        // The group labels the FormSchema and SystemSettings editors add; the annotations cannot carry them.
        private static readonly string[] s_editorGroupLabels = ["Relation", "Lookup", "ExtendedProperties"];

        private static IEnumerable<string> FixedAnnotationLabels() =>
            typeof(TreeNodeAttribute).Assembly.GetTypes()
                .Select(t => t.GetCustomAttribute<TreeNodeAttribute>(inherit: false))
                .Where(a => a != null && string.IsNullOrEmpty(a.PropertyName) && !string.IsNullOrEmpty(a.DisplayFormat))
                .Select(a => a!.DisplayFormat)
                .Distinct(StringComparer.Ordinal);

        [Fact]
        [DisplayName("Translate returns a label without a resource as written")]
        public void Translate_NoResource_ReturnsLabel()
        {
            Assert.Equal("{0} - {1}", TreeLabels.Translate("{0} - {1}"));
            Assert.Equal("No Such Label", TreeLabels.Translate("No Such Label"));
        }

        [Fact]
        [DisplayName("Every fixed TreeNode label and editor group label has English and Traditional Chinese text")]
        public void Resources_CoverEveryFixedLabel()
        {
            var zhTw = CultureInfo.GetCultureInfo("zh-TW");
            var missing = FixedAnnotationLabels().Concat(s_editorGroupLabels)
                .Select(label => "TreeNode_" + label.Replace(" ", string.Empty, StringComparison.Ordinal))
                .Where(key => s_resources.GetString(key, CultureInfo.InvariantCulture) is null
                    || s_resources.GetString(key, zhTw) == s_resources.GetString(key, CultureInfo.InvariantCulture))
                .ToList();

            Assert.Empty(missing);
        }
    }
}
