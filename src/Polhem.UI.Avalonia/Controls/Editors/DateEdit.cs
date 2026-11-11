using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.DataObjects;
using Polhem.UI.Core.Editing;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.DateEdit"/>: a <see cref="DatePicker"/>
    /// two-way bound to a <see cref="FormDataObject"/> field using the ISO
    /// <c>yyyy-MM-dd</c> binding format.
    /// </summary>
    public class DateEdit : DatePicker, IFieldEditor, IFieldEditorBinderHost
    {
        /// <summary>
        /// Identifies the <see cref="FieldName"/> styled property.
        /// </summary>
        public static readonly StyledProperty<string> FieldNameProperty =
            AvaloniaProperty.Register<DateEdit, string>(nameof(FieldName), string.Empty);

        /// <summary>
        /// Identifies the <see cref="ReadOnlyText"/> styled property.
        /// </summary>
        public static readonly StyledProperty<string?> ReadOnlyTextProperty =
            AvaloniaProperty.Register<DateEdit, string?>(nameof(ReadOnlyText));

        // The read-only view swaps the whole DatePicker template for a flat underlined
        // label: the picker has no read-only mode that hides the calendar button without
        // greying out, and restyling theme-specific template parts is not portable across
        // Fluent / Semi. The label tracks `ReadOnlyText`; the native template returns on
        // ClearValue when the field becomes editable again.
        private static readonly FuncControlTemplate<DateEdit> s_readOnlyTemplate =
            new((control, scope) => ReadOnlyFieldVisual.Build(
                control.GetObservable(ReadOnlyTextProperty), scope, ReadOnlyFieldVisual.HostKind.DatePicker));

        private readonly FieldEditorBinder _binder;
        // The caption this editor last applied as its accessible name, so a later caption may replace it.
        private string? _captionName;

        FieldEditorBinder IFieldEditorBinderHost.Binder => _binder;

        static DateEdit()
        {
            FieldNameProperty.Changed.AddClassHandler<DateEdit>((o, _) => o._binder.OnBindingContextChanged());
            FormScope.DataObjectProperty.Changed.AddClassHandler<DateEdit>((o, _) => o._binder.OnBindingContextChanged());
            FormScope.FormModeProperty.Changed.AddClassHandler<DateEdit>((o, e) => o._binder.OnFormModeChanged((SingleFormMode)e.NewValue!));
        }

        /// <summary>
        /// Initializes a new instance of <see cref="DateEdit"/>.
        /// </summary>
        public DateEdit()
        {
            // Fill the cell width like the TextBox-based editors (whose base already
            // stretches), so the field keeps a fixed width and the read-only underline
            // spans the whole field rather than the picker's content width.
            HorizontalAlignment = HorizontalAlignment.Stretch;
            _binder = new FieldEditorBinder(this, RefreshFromSource, ApplyMetadata);
            TemplateApplied += OnTemplateApplied;
            SelectedDateChanged += OnSelectedDateChangedCore;
        }

        /// <inheritdoc />
        // WARNING: Without this override the subclass looks up a ControlTheme keyed by
        // its own type, which the application theme does not provide, and the control
        // renders with no visual at all.
        protected override Type StyleKeyOverride => typeof(DatePicker);

        /// <summary>
        /// Gets the format used to render the selected date back into the bound field.
        /// </summary>
        protected virtual string ValueFormat => "yyyy-MM-dd";

        /// <summary>
        /// Gets the standard format the read-only display renders the date with in the user's
        /// culture (<see cref="CultureInfo.CurrentCulture"/>). Defaults to the short date, <c>d</c>.
        /// </summary>
        protected virtual string DisplayFormat => "d";

        /// <summary>
        /// Gets or sets the bound field (column) name.
        /// </summary>
        public string FieldName
        {
            get => GetValue(FieldNameProperty);
            set => SetValue(FieldNameProperty, value);
        }

        /// <summary>
        /// Gets or sets the formatted text shown by the read-only display template.
        /// </summary>
        public string? ReadOnlyText
        {
            get => GetValue(ReadOnlyTextProperty);
            set => SetValue(ReadOnlyTextProperty, value);
        }

        /// <summary>
        /// Gets or sets the editor value as the bound field value.
        /// </summary>
        public object? FieldValue
        {
            get => SelectedDate;
            set => SelectedDate = value switch
            {
                null => null,
                DateTimeOffset dto => dto,
                DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt.Date, DateTimeKind.Unspecified), TimeSpan.Zero),
                _ => ParseToOffset(value.ToString()),
            };
        }

        /// <inheritdoc />
        public void Bind(FormDataObject dataObject, LayoutField field)
        {
            ArgumentNullException.ThrowIfNull(field);
            _binder.BindExplicit(dataObject, field.FieldName, field);
        }

        /// <inheritdoc />
        public void Bind(FormDataObject dataObject, string fieldName)
        {
            _binder.BindExplicit(dataObject, fieldName, layoutField: null);
        }

        /// <inheritdoc />
        public void Bind(FormDataObject dataObject, LayoutFieldBase field, System.Data.DataRow row)
        {
            _binder.BindRow(dataObject, field, row);
        }

        /// <inheritdoc />
        public void Unbind()
        {
            _binder.Unbind();
        }

        /// <inheritdoc />
        public void SetControlState(SingleFormMode formMode)
        {
            if (_binder.AllowsEdit(formMode))
            {
                ClearValue(TemplateProperty);
                ClearValue(FocusableProperty);
                ClearValue(IsHitTestVisibleProperty);
            }
            else
            {
                // Swap to the flat underlined display: no calendar button, no grey-out.
                Template = s_readOnlyTemplate;
                Focusable = false;
                IsHitTestVisible = false;
            }
        }

        /// <inheritdoc />
        protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnAttachedToLogicalTree(e);
            _binder.NotifyAttached();
        }

        /// <inheritdoc />
        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            _binder.NotifyDetached();
        }

        private void OnSelectedDateChangedCore(object? sender, DatePickerSelectedValueChangedEventArgs e)
        {
            if (e.NewDate is { } date)
                _binder.WriteBack(date.DateTime.ToString(ValueFormat, CultureInfo.InvariantCulture));
            else
                _binder.WriteBack(null);
        }

        private void RefreshFromSource()
        {
            var date = ParseToOffset(_binder.GetValue());
            SelectedDate = date;
            // Shown to the user, so in the user's culture; the bound value keeps the invariant form.
            ReadOnlyText = date?.DateTime.ToString(DisplayFormat, CultureInfo.CurrentCulture) ?? string.Empty;
        }

        private void ApplyMetadata()
        {
            // The read-only view state is applied by SetControlState, which the binder
            // runs immediately after this on bind and on every form-mode change.
            ApplyAccessibleName();
        }

        // The picker is a button whose content is a grid of date parts, so without a name assistive technology reads the
        // grid's type name. The field's caption names it, unless the application set a name of its own.
        private void ApplyAccessibleName()
        {
            var caption = _binder.LayoutField?.Caption is { Length: > 0 } layoutCaption
                ? layoutCaption
                : _binder.FormField?.Caption;
            var current = AutomationProperties.GetName(this);
            if (string.IsNullOrEmpty(caption)
                || !(string.IsNullOrEmpty(current) || string.Equals(current, _captionName, StringComparison.Ordinal)))
                return;
            _captionName = caption;
            AutomationProperties.SetName(this, caption);
        }

        // The read-only template has no flyout button; the editable one gets the editor's name.
        private void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            if (e.NameScope.Find<Button>("PART_FlyoutButton") is { } button)
                button[!AutomationProperties.NameProperty] = this[!AutomationProperties.NameProperty];
        }

        // NOTE: Internal (not private) so GridControl's in-cell date editors share the
        // same DateTimeKind handling.
        internal static DateTimeOffset? ParseToOffset(string? raw)
        {
            if (!DateTime.TryParse(
                    raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var parsed))
            {
                return null;
            }

            // `AssumeLocal` yields Kind=Local, and the DateTimeOffset(DateTime, TimeSpan)
            // constructor rejects a Local value whose offset argument differs from the
            // machine's UTC offset — so pinning TimeSpan.Zero throws everywhere outside
            // UTC. Strip the kind first; the picker only consumes the date component.
            var dateOnly = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Unspecified);
            return new DateTimeOffset(dateOnly, TimeSpan.Zero);
        }
    }
}
