using System.ComponentModel;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    public class PropertyGridMetadataTests
    {
        private static readonly string[] s_suggestions = ["Alpha", "Beta"];

        private static PropertyDescriptor Property(string name) =>
            TypeDescriptor.GetProperties(typeof(PropertyGridSample))[name]!;

        [Fact]
        [DisplayName("GetProperties leaves out a property marked Browsable(false)")]
        public void GetProperties_BrowsableFalse_IsLeftOut()
        {
            var names = PropertyGridMetadata.GetProperties(new PropertyGridSample()).Select(p => p.Name).ToList();

            Assert.DoesNotContain(nameof(PropertyGridSample.Hidden), names);
            Assert.Contains(nameof(PropertyGridSample.Name), names);
        }

        [Fact]
        [DisplayName("GroupByCategory orders the groups by first appearance and puts an uncategorized property under Misc")]
        public void GroupByCategory_OrdersGroupsByFirstAppearance()
        {
            var groups = PropertyGridMetadata.GroupByCategory(PropertyGridMetadata.GetProperties(new PropertyGridSample()));

            Assert.Equal(["General", "Layout", "Misc"], groups.Select(g => g.Key));
            Assert.Equal(nameof(PropertyGridSample.Count), Assert.Single(groups[2]).Name);
        }

        [Theory]
        [InlineData(nameof(PropertyGridSample.Name), nameof(PropertyGridEditorKind.Text))]
        [InlineData(nameof(PropertyGridSample.Enabled), nameof(PropertyGridEditorKind.Boolean))]
        [InlineData(nameof(PropertyGridSample.Mode), nameof(PropertyGridEditorKind.Choice))]
        [InlineData(nameof(PropertyGridSample.Count), nameof(PropertyGridEditorKind.Numeric))]
        [InlineData(nameof(PropertyGridSample.When), nameof(PropertyGridEditorKind.Date))]
        [InlineData(nameof(PropertyGridSample.Items), nameof(PropertyGridEditorKind.Collection))]
        [InlineData(nameof(PropertyGridSample.Child), nameof(PropertyGridEditorKind.Summary))]
        [InlineData(nameof(PropertyGridSample.Flags), nameof(PropertyGridEditorKind.Text))]
        [DisplayName("GetEditorKind picks the editor by the property's type and converter")]
        public void GetEditorKind_PicksByType(string name, string expected)
        {
            Assert.Equal(Enum.Parse<PropertyGridEditorKind>(expected), PropertyGridMetadata.GetEditorKind(Property(name)));
        }

        [Theory]
        [InlineData(nameof(FormField.ListItems))]
        [InlineData(nameof(FormField.RelationFieldMappings))]
        [InlineData(nameof(FormField.LookupFieldMappings))]
        [DisplayName("The collections of FormField that the grid shows get the collection editor kind")]
        public void GetEditorKind_FormFieldCollections_AreCollections(string name)
        {
            var property = TypeDescriptor.GetProperties(typeof(FormField))[name]!;

            Assert.True(property.IsBrowsable);
            Assert.Equal(PropertyGridEditorKind.Collection, PropertyGridMetadata.GetEditorKind(property));
        }

        [Theory]
        [InlineData(typeof(ListItemCollection), typeof(ListItem))]
        [InlineData(typeof(FieldMappingCollection), typeof(FieldMapping))]
        [InlineData(typeof(List<string>), typeof(string))]
        [InlineData(typeof(System.Collections.ArrayList), typeof(object))]
        [DisplayName("GetItemType reads the item type from the collection base, then IList<T>, else object")]
        public void GetItemType_ResolvesItemType(Type collectionType, Type expected)
        {
            Assert.Equal(expected, PropertyGridMetadata.GetItemType(collectionType));
        }

        [Fact]
        [DisplayName("GetChoices of an enum property lists the enum's members")]
        public void GetChoices_Enum_ListsMembers()
        {
            Assert.Equal(
                [PropertyGridSample.SampleMode.First, PropertyGridSample.SampleMode.Second],
                PropertyGridMetadata.GetChoices(Property(nameof(PropertyGridSample.Mode))));
        }

        [Fact]
        [DisplayName("IsModified is true only once a value differs from its DefaultValue")]
        public void IsModified_DefaultValue_FollowsTheValue()
        {
            var sample = new PropertyGridSample();
            var property = Property(nameof(PropertyGridSample.Name));
            Assert.False(PropertyGridMetadata.IsModified(property, sample));

            sample.Name = "changed";

            Assert.True(PropertyGridMetadata.IsModified(property, sample));
        }

        [Fact]
        [DisplayName("IsModified is false for a property without DefaultValue, whatever its value")]
        public void IsModified_NoDefaultValue_IsFalse()
        {
            var sample = new PropertyGridSample { NoDefault = "anything" };

            Assert.False(PropertyGridMetadata.IsModified(Property(nameof(PropertyGridSample.NoDefault)), sample));
        }

        [Fact]
        [DisplayName("IsModified of a collection is true only while it has items")]
        public void IsModified_Collection_FollowsTheCount()
        {
            var sample = new PropertyGridSample();
            var property = Property(nameof(PropertyGridSample.Items));
            Assert.False(PropertyGridMetadata.IsModified(property, sample));

            sample.Items.Add("one");

            Assert.True(PropertyGridMetadata.IsModified(property, sample));
        }

        [Fact]
        [DisplayName("No property of BackendConfiguration shows as modified on a new instance, though most have no DefaultValue")]
        public void IsModified_BackendConfiguration_NothingInBold()
        {
            var settings = new BackendConfiguration();

            var modified = PropertyGridMetadata.GetProperties(settings)
                .Where(p => PropertyGridMetadata.IsModified(p, settings))
                .Select(p => p.Name);

            Assert.Empty(modified);
        }

        [Fact]
        [DisplayName("CanReset is false when the grid is read-only or the property has no setter")]
        public void CanReset_ReadOnly_IsFalse()
        {
            var sample = new PropertyGridSample { Name = "changed" };
            var name = Property(nameof(PropertyGridSample.Name));

            Assert.True(PropertyGridMetadata.CanReset(name, sample, gridReadOnly: false));
            Assert.False(PropertyGridMetadata.CanReset(name, sample, gridReadOnly: true));
            Assert.False(PropertyGridMetadata.CanReset(Property(nameof(PropertyGridSample.Computed)), sample, gridReadOnly: false));
        }

        [Fact]
        [DisplayName("TryParseText converts valid text through the property's converter")]
        public void TryParseText_ValidText_Converts()
        {
            var ok = PropertyGridMetadata.TryParseText(Property(nameof(PropertyGridSample.Flags)), "A, B", out var value, out var error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.Equal(PropertyGridSample.SampleOptions.A | PropertyGridSample.SampleOptions.B, value);
        }

        [Fact]
        [DisplayName("TryParseText reports text the converter refuses instead of throwing")]
        public void TryParseText_InvalidText_ReportsError()
        {
            var ok = PropertyGridMetadata.TryParseText(Property(nameof(PropertyGridSample.Flags)), "Nope", out _, out var error);

            Assert.False(ok);
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        [DisplayName("TrySetValue reports the setter's own message when the setter refuses the value")]
        public void TrySetValue_SetterRefuses_ReportsItsMessage()
        {
            var sample = new PropertyGridSample();

            var ok = PropertyGridMetadata.TrySetValue(Property(nameof(PropertyGridSample.Guarded)), sample, "bad", out var error);

            Assert.False(ok);
            Assert.StartsWith("The value is refused.", error);
            Assert.Equal(string.Empty, sample.Guarded);
        }

        [Fact]
        [DisplayName("TryResetValue puts a property back to its DefaultValue")]
        public void TryResetValue_RestoresDefault()
        {
            var sample = new PropertyGridSample { Mode = PropertyGridSample.SampleMode.Second };

            var ok = PropertyGridMetadata.TryResetValue(Property(nameof(PropertyGridSample.Mode)), sample, out _);

            Assert.True(ok);
            Assert.Equal(PropertyGridSample.SampleMode.First, sample.Mode);
        }

        [Fact]
        [DisplayName("ToPickerDate takes DateTime.MinValue without applying a local offset")]
        public void ToPickerDate_MinValue_DoesNotThrow()
        {
            var date = PropertyGridMetadata.ToPickerDate(DateTime.MinValue);

            Assert.Equal(new DateTimeOffset(1, 1, 1, 0, 0, 0, TimeSpan.Zero), date);
        }

        [Fact]
        [DisplayName("FromPickerDate keeps the time of day and the kind of the DateTime it replaces")]
        public void FromPickerDate_DateTime_KeepsTimeAndKind()
        {
            var original = new DateTime(2020, 1, 2, 13, 45, 0, DateTimeKind.Utc);

            var value = (DateTime)PropertyGridMetadata.FromPickerDate(
                typeof(DateTime), new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero), original);

            Assert.Equal(new DateTime(2026, 10, 6, 13, 45, 0, DateTimeKind.Utc), value);
            Assert.Equal(DateTimeKind.Utc, value.Kind);
        }

        [Fact]
        [DisplayName("FromPickerDate returns a DateOnly for a DateOnly property")]
        public void FromPickerDate_DateOnly_ReturnsDateOnly()
        {
            var value = PropertyGridMetadata.FromPickerDate(
                typeof(DateOnly), new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero), null);

            Assert.Equal(new DateOnly(2026, 10, 6), value);
        }

        [Fact]
        [DisplayName("FromNumber returns null for a number the property's type cannot hold")]
        public void FromNumber_Overflow_ReturnsNull()
        {
            Assert.Null(PropertyGridMetadata.FromNumber(typeof(byte), 300));
            Assert.Equal((byte)42, PropertyGridMetadata.FromNumber(typeof(byte), 42));
        }

        [Fact]
        [DisplayName("GetNumericRange limits an integral type to its own range with a whole-number format")]
        public void GetNumericRange_Integral_UsesTypeRange()
        {
            var (minimum, maximum, format) = PropertyGridMetadata.GetNumericRange(typeof(short));

            Assert.Equal(short.MinValue, minimum);
            Assert.Equal(short.MaxValue, maximum);
            Assert.Equal("0", format);
        }

        [Fact]
        [DisplayName("Translate keeps the text when there is no translator or it returns null or an empty string")]
        public void Translate_NoTranslation_KeepsText()
        {
            Assert.Equal("Name", Translate(null));
            Assert.Equal("Name", Translate(_ => string.Empty));
            Assert.Equal("Name", Translate(_ => null));
            Assert.Equal("NAME", Translate(t => t.Text.ToUpperInvariant()));

            static string Translate(Func<PropertyGridText, string?>? translator) => PropertyGridMetadata.Translate(
                translator, PropertyGridTextKind.DisplayName, typeof(PropertyGridSample), "Name", "Name");
        }

        [Fact]
        [DisplayName("Translate hands the translator the kind, component type, property name and text")]
        public void Translate_PassesContext()
        {
            PropertyGridText? received = null;

            PropertyGridMetadata.Translate(t => { received = t; return null; }, PropertyGridTextKind.Description,
                typeof(FormField), nameof(FormField.Caption), "The caption.");

            Assert.NotNull(received);
            Assert.Equal(PropertyGridTextKind.Description, received.Kind);
            Assert.Equal(typeof(FormField), received.ComponentType);
            Assert.Equal(nameof(FormField.Caption), received.PropertyName);
            Assert.Equal("The caption.", received.Text);
        }

        [Fact]
        [DisplayName("FindTakenKey reports a key another item of the collection has, ignoring case, and nothing else")]
        public void FindTakenKey_ReportsOnlyAnotherItemsKey()
        {
            var items = new ListItemCollection { { "A", "Active" }, { "S", "Suspended" } };
            var active = items["A"];

            Assert.Equal("s", PropertyGridMetadata.FindTakenKey(active, "s"));
            Assert.Null(PropertyGridMetadata.FindTakenKey(active, "A"));
            Assert.Null(PropertyGridMetadata.FindTakenKey(active, "C"));
            Assert.Null(PropertyGridMetadata.FindTakenKey(new ListItem("X", "Loose"), "A"));
            Assert.Null(PropertyGridMetadata.FindTakenKey(new PropertyGridSample(), "A"));
        }

        [Fact]
        [DisplayName("IsPassword is true only for a property marked PasswordPropertyText(true)")]
        public void IsPassword_FollowsAttribute()
        {
            Assert.True(PropertyGridMetadata.IsPassword(Property(nameof(PropertyGridSample.Secret))));
            Assert.False(PropertyGridMetadata.IsPassword(Property(nameof(PropertyGridSample.Name))));
        }

        [Fact]
        [DisplayName("GetSuggestions asks the provider only for a string text property that is not a password")]
        public void GetSuggestions_AsksOnlyForPlainStringText()
        {
            var sample = new PropertyGridSample();
            var asked = new List<string>();
            IReadOnlyList<string>? Provider(PropertyDescriptor property, object component)
            {
                asked.Add(property.Name);
                return s_suggestions;
            }

            foreach (var property in PropertyGridMetadata.GetProperties(sample))
                PropertyGridMetadata.GetSuggestions(property, sample, Provider);

            Assert.Contains(nameof(PropertyGridSample.Name), asked);
            Assert.DoesNotContain(nameof(PropertyGridSample.Secret), asked);
            Assert.DoesNotContain(nameof(PropertyGridSample.Count), asked);
            Assert.DoesNotContain(nameof(PropertyGridSample.Flags), asked);
            Assert.Same(s_suggestions, PropertyGridMetadata.GetSuggestions(Property(nameof(PropertyGridSample.Name)), sample, Provider));
            Assert.Null(PropertyGridMetadata.GetSuggestions(Property(nameof(PropertyGridSample.Name)), sample, null));
        }

        [Fact]
        [DisplayName("A flags enum whose converter offers exclusive values gets a drop-down of those values")]
        public void GetEditorKind_FlagsWithExclusiveConverter_IsChoice()
        {
            var action = TypeDescriptor.GetProperties(typeof(PermissionRule))[nameof(PermissionRule.Action)]!;

            Assert.Equal(PropertyGridEditorKind.Choice, PropertyGridMetadata.GetEditorKind(action));
            Assert.Equal(
                Enum.GetValues<PermissionActions>().Where(a => a != PermissionActions.None).Cast<object>(),
                PropertyGridMetadata.GetChoices(action));
        }
    }
}
