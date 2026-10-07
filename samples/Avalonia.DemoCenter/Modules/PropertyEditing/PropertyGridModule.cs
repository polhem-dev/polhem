using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.DemoCenter.Modules.DataEditors;
using Avalonia.Layout;
using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Controls;

namespace Avalonia.DemoCenter.Modules.PropertyEditing
{
    /// <summary>
    /// <see cref="PropertyGridControl"/> over real definition objects: the grid reads the
    /// <c>System.ComponentModel</c> annotations on each type, so nothing here describes the properties.
    /// </summary>
    /// <remarks>
    /// The options on the left switch the object and the grid's display settings; the log under them lists each
    /// <see cref="PropertyGridControl.PropertyValueChanged"/>. The translator option upper-cases every label to show
    /// where <see cref="PropertyGridControl.LabelTranslator"/> applies, and the suggestion option offers form ids for
    /// the FormField's <c>RelationProgId</c> and <c>LookupProgId</c> through
    /// <see cref="PropertyGridControl.ValueSuggestionProvider"/>.
    /// </remarks>
    public sealed class PropertyGridModule : DemoModuleBase
    {
        private static readonly string[] s_progIds = ["Customer", "Employee", "Product", "Supplier"];

        /// <inheritdoc/>
        public override string Category => "Property Grid";

        /// <inheritdoc/>
        public override string Title => "PropertyGridControl over definition objects";

        /// <inheritdoc/>
        public override string Description =>
            "Pick a FormField, a DbField, a DatabaseServer, a PermissionRule or BackendConfiguration: the rows, groups, "
            + "editors and descriptions all come from "
            + "[Category], [Description], [DefaultValue], [Browsable] and the property types. A value that differs from its "
            + "default is in bold, and right-clicking its label resets it. The … button of a collection, such as the "
            + "FormField's ListItems, opens CollectionEditDialog: add, delete and reorder the items and edit the selected one "
            + "in a grid of its own; Cancel leaves the collection as it was. Nested settings objects show their text. The "
            + "DatabaseServer's Password is masked, the PermissionRule's Action offers single actions, and the suggestion "
            + "option turns the FormField's RelationProgId and LookupProgId into drop-downs that still take typed text.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var subjects = new (string Name, object Value)[]
            {
                ("FormField (DropDownEdit with ListItems)", CreateFormField()),
                ("DbField", new DbField("unit_price", "Unit price", FieldDbType.Decimal) { Precision = 18, Scale = 2 }),
                ("DatabaseServer (masked Password)", new DatabaseServer { Id = "main", DisplayName = "Main server", Password = "demo-only" }),
                ("PermissionRule", new PermissionRule(PermissionActions.Read, ScopeStrategy.Own)),
                ("BackendConfiguration", new BackendConfiguration()),
            };

            var grid = new PropertyGridControl { Height = 560, SelectedObject = subjects[0].Value };
            var log = new ObservableCollection<string>();
            // A password row hides its value, so the log does not show it either.
            grid.PropertyValueChanged += (_, e) =>
                log.Insert(0, e.NewValue is ICollection items
                    ? $"{e.Property.Name}: now {items.Count} items"
                    : e.Property.Attributes[typeof(PasswordPropertyTextAttribute)] is PasswordPropertyTextAttribute { Password: true }
                        ? $"{e.Property.Name}: changed"
                        : $"{e.Property.Name}: {e.OldValue ?? "(null)"} → {e.NewValue ?? "(null)"}");

            var subject = new ComboBox
            {
                ItemsSource = subjects.Select(s => s.Name).ToList(),
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            subject.SelectionChanged += (_, _) =>
            {
                if (subject.SelectedIndex >= 0)
                    grid.SelectedObject = subjects[subject.SelectedIndex].Value;
            };

            var categories = new CheckBox { Content = "ShowCategories", IsChecked = true };
            categories.IsCheckedChanged += (_, _) => grid.ShowCategories = categories.IsChecked == true;
            var description = new CheckBox { Content = "ShowDescription", IsChecked = true };
            description.IsCheckedChanged += (_, _) => grid.ShowDescription = description.IsChecked == true;
            var readOnly = new CheckBox { Content = "IsReadOnly" };
            readOnly.IsCheckedChanged += (_, _) => grid.IsReadOnly = readOnly.IsChecked == true;
            var translate = new CheckBox { Content = "LabelTranslator (upper case)" };
            translate.IsCheckedChanged += (_, _) =>
                grid.LabelTranslator = translate.IsChecked == true ? text => text.Text.ToUpperInvariant() : null;
            var suggest = new CheckBox { Content = "ValueSuggestionProvider (form ids)" };
            suggest.IsCheckedChanged += (_, _) =>
                grid.ValueSuggestionProvider = suggest.IsChecked == true ? SuggestProgIds : null;

            var options = new StackPanel
            {
                Spacing = 10,
                Width = 280,
                Children =
                {
                    DataEditorParts.Section("Object", null, subject),
                    DataEditorParts.Section("Display", null, categories, description, readOnly, translate, suggest),
                    DataEditorParts.Section("PropertyValueChanged", null,
                        new ListBox { ItemsSource = log, Height = 200 }),
                },
            };

            var root = new Grid { Margin = new Thickness(4), ColumnSpacing = 24 };
            root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var gridSection = DataEditorParts.Section("PropertyGridControl", null, grid);
            Grid.SetColumn(options, 0);
            Grid.SetColumn(gridSection, 1);
            root.Children.Add(options);
            root.Children.Add(gridSection);
            return new ScrollViewer { Content = root };
        }

        private static IReadOnlyList<string>? SuggestProgIds(PropertyDescriptor property, object component)
        {
            return component is FormField && property.Name is nameof(FormField.RelationProgId) or nameof(FormField.LookupProgId)
                ? s_progIds
                : null;
        }

        private static FormField CreateFormField()
        {
            var field = new FormField("status", "Status", FieldDbType.String)
            {
                ControlType = ControlType.DropDownEdit,
                MaxLength = 1,
            };
            field.ListItems.Add("A", "Active");
            field.ListItems.Add("S", "Suspended");
            field.ListItems.Add("C", "Closed");
            return field;
        }
    }
}
