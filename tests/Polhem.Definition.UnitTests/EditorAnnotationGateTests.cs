using System.ComponentModel;
using System.Reflection;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Checks that every property a property grid shows on a definition type says where it belongs and what it means.
    /// </summary>
    /// <remarks>
    /// The DefineEditor builds its property panels from these annotations, so a property without <c>[Category]</c> lands
    /// in the BCL's catch-all <c>Misc</c> group, and one without <c>[Description]</c> leaves the description bar empty.
    /// The types are the ones <see cref="XmlSerializerShapeGateTests"/> walks from the definition file roots.
    /// </remarks>
    public class EditorAnnotationGateTests
    {
        [Fact]
        [DisplayName("Every browsable property of a definition type declares Category and Description")]
        public void BrowsableDefinitionProperties_DeclareCategoryAndDescription()
        {
            var types = XmlSerializerShapeGateTests.Walk()
                .Where(t => !XmlSerializerShapeGateTests.IsCollection(t))
                .ToList();
            var missing = new List<string>();
            var checkedProperties = 0;

            foreach (var type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                foreach (var descriptor in TypeDescriptor.GetProperties(type, [BrowsableAttribute.Yes]).Cast<PropertyDescriptor>())
                {
                    // NOTE: The descriptor's own Category and Description cannot be used. Category falls back to "Misc"
                    // when nothing is declared, and both merge in the attributes of the property's type, so a
                    // class-level [Description] would pass for every property of that type.
                    var property = DeclaredProperty(type, descriptor.Name);
                    if (property is null) { continue; }
                    checkedProperties++;
                    var lacks = new List<string>();
                    if (Attribute.GetCustomAttribute(property, typeof(CategoryAttribute), inherit: true) is null)
                        lacks.Add("[Category]");
                    if (Attribute.GetCustomAttribute(property, typeof(DescriptionAttribute), inherit: true) is not DescriptionAttribute { Description.Length: > 0 })
                        lacks.Add("[Description]");
                    if (lacks.Count > 0)
                        missing.Add($"{type.FullName}.{property.Name}: {string.Join(", ", lacks)}");
                }
            }

            // Anti-vacuous: the walk must still reach the types the editor shows.
            Assert.True(types.Count > 40, $"Only {types.Count} definition types were checked.");
            Assert.True(checkedProperties > 200, $"Only {checkedProperties} properties were checked.");
            Assert.True(
                missing.Count == 0,
                $"These browsable definition properties lack editor annotations; add them, or [Browsable(false)] when the "
                + $"editor should not show the property:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
        }

        [Fact]
        [DisplayName("LanguageItem.Key stays browsable although the base Key it overrides is hidden")]
        public void LanguageItemKey_IsBrowsable()
        {
            var key = TypeDescriptor.GetProperties(typeof(Polhem.Definition.Language.LanguageItem))[nameof(Polhem.Definition.Language.LanguageItem.Key)];

            Assert.NotNull(key);
            Assert.True(key.IsBrowsable);
            Assert.Equal(PropertyCategories.Data, key.Category);
        }

        [Theory]
        [DisplayName("The encryption keys of SecurityKeySettings are marked for a masked editor")]
        [InlineData(nameof(Polhem.Definition.Settings.SecurityKeySettings.ApiEncryptionKey))]
        [InlineData(nameof(Polhem.Definition.Settings.SecurityKeySettings.CookieEncryptionKey))]
        [InlineData(nameof(Polhem.Definition.Settings.SecurityKeySettings.ConfigEncryptionKey))]
        [InlineData(nameof(Polhem.Definition.Settings.SecurityKeySettings.DatabaseEncryptionKey))]
        public void SecurityKeys_AreMarkedAsPasswords(string propertyName)
        {
            var property = TypeDescriptor.GetProperties(typeof(Polhem.Definition.Settings.SecurityKeySettings))[propertyName];

            Assert.True(property?.Attributes[typeof(PasswordPropertyTextAttribute)] is PasswordPropertyTextAttribute { Password: true });
        }

        /// <summary>
        /// The most derived public instance property of <paramref name="type"/> named <paramref name="name"/>.
        /// </summary>
        private static PropertyInfo? DeclaredProperty(Type type, string name)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                var property = current.GetProperty(name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property is not null) { return property; }
            }
            return null;
        }
    }
}
