using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// The UI-free decisions of <see cref="PropertyGridControl"/>: which properties it shows and in which groups, which
    /// editor each one gets, when a value counts as changed, and how text converts to and from a value.
    /// </summary>
    internal static class PropertyGridMetadata
    {
        private static readonly HashSet<Type> s_numericTypes =
        [
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
            typeof(long), typeof(ulong), typeof(decimal),
        ];

        /// <summary>
        /// Returns the browsable properties of <paramref name="component"/> in the order
        /// <see cref="TypeDescriptor"/> returns them.
        /// </summary>
        /// <remarks>
        /// Properties go through <see cref="TypeDescriptor"/>, not plain reflection, so a <c>[TypeConverter]</c>, a
        /// custom type descriptor and <c>[Browsable(false)]</c> all apply.
        /// </remarks>
        internal static IReadOnlyList<PropertyDescriptor> GetProperties(object component)
        {
            ArgumentNullException.ThrowIfNull(component);
            return TypeDescriptor.GetProperties(component, [BrowsableAttribute.Yes])
                .Cast<PropertyDescriptor>()
                .ToList();
        }

        /// <summary>
        /// Groups <paramref name="properties"/> by <see cref="MemberDescriptor.Category"/>, the groups in the order their
        /// first property appears and the properties in their given order.
        /// </summary>
        /// <remarks>
        /// A property without <c>[Category]</c> reports the BCL's default category, <c>Misc</c>.
        /// </remarks>
        internal static IReadOnlyList<IGrouping<string, PropertyDescriptor>> GroupByCategory(
            IEnumerable<PropertyDescriptor> properties)
        {
            return properties.GroupBy(p => p.Category, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Decides which editor <paramref name="property"/> gets.
        /// </summary>
        internal static PropertyGridEditorKind GetEditorKind(PropertyDescriptor property)
        {
            ArgumentNullException.ThrowIfNull(property);
            var type = property.PropertyType;
            if (IsCollectionType(type)) { return PropertyGridEditorKind.Collection; }
            if (type == typeof(bool)) { return PropertyGridEditorKind.Boolean; }
            if (IsPlainEnum(type)) { return PropertyGridEditorKind.Choice; }
            if (s_numericTypes.Contains(type)) { return PropertyGridEditorKind.Numeric; }
            if (type == typeof(DateTime) || type == typeof(DateOnly)) { return PropertyGridEditorKind.Date; }

            // A flags enum takes several members at once, which a drop-down cannot pick: its default converter offers
            // no exclusive values and reads "A, B" from text. A converter on the property that offers exclusive values,
            // such as one that allows a single member, still gets a drop-down here.
            var converter = property.Converter;
            if (converter.GetStandardValuesSupported() && converter.GetStandardValuesExclusive()
                && converter.GetStandardValues() is { Count: > 0 })
            {
                return PropertyGridEditorKind.Choice;
            }
            // A value whose text cannot be read back, such as a nested object behind an ExpandableObjectConverter,
            // would be written over by its ToString() text, so it is only shown.
            return converter.CanConvertFrom(typeof(string))
                ? PropertyGridEditorKind.Text
                : PropertyGridEditorKind.Summary;
        }

        /// <summary>
        /// Returns whether a <see cref="PropertyGridEditorKind.Text"/> row of <paramref name="property"/> hides its text,
        /// as <c>[PasswordPropertyText(true)]</c> asks.
        /// </summary>
        internal static bool IsPassword(PropertyDescriptor property)
        {
            ArgumentNullException.ThrowIfNull(property);
            return property.Attributes[typeof(PasswordPropertyTextAttribute)] is PasswordPropertyTextAttribute { Password: true };
        }

        /// <summary>
        /// Returns the values <paramref name="provider"/> suggests for <paramref name="property"/> on
        /// <paramref name="component"/>, or <c>null</c> when the row stays a plain text box.
        /// </summary>
        /// <remarks>
        /// Only a <see cref="string"/> property that would get a <see cref="PropertyGridEditorKind.Text"/> row is asked,
        /// and never a password, whose text a drop-down would show.
        /// </remarks>
        internal static IReadOnlyList<string>? GetSuggestions(PropertyDescriptor property, object component,
            Func<PropertyDescriptor, object, IReadOnlyList<string>?>? provider)
        {
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(component);
            if (provider is null || property.PropertyType != typeof(string) || IsPassword(property)
                || GetEditorKind(property) != PropertyGridEditorKind.Text)
            {
                return null;
            }
            return provider(property, component);
        }

        /// <summary>
        /// Returns whether <paramref name="type"/> is shown as a collection: it implements <see cref="IList"/> and is not
        /// a <see cref="string"/>.
        /// </summary>
        internal static bool IsCollectionType(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return type != typeof(string) && typeof(IList).IsAssignableFrom(type);
        }

        /// <summary>
        /// Returns the type of the items of <paramref name="collectionType"/>: the type argument of
        /// <see cref="Polhem.Core.Collections.CollectionBase{T}"/> or <see cref="Polhem.Core.Collections.KeyCollectionBase{T}"/>
        /// when the type derives from one, else the item type of the <see cref="IList{T}"/> it implements, else
        /// <see cref="object"/>.
        /// </summary>
        internal static Type GetItemType(Type collectionType)
        {
            ArgumentNullException.ThrowIfNull(collectionType);
            for (var type = collectionType; type is not null; type = type.BaseType)
            {
                if (!type.IsGenericType) { continue; }
                var definition = type.GetGenericTypeDefinition();
                if (definition == typeof(Polhem.Core.Collections.CollectionBase<>)
                    || definition == typeof(Polhem.Core.Collections.KeyCollectionBase<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }
            var list = collectionType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
            return list?.GetGenericArguments()[0] ?? typeof(object);
        }

        /// <summary>
        /// Returns whether items of <paramref name="itemType"/> can be edited in the property grid of the collection
        /// dialog: a class other than <see cref="string"/>, whose properties are what the dialog edits.
        /// </summary>
        internal static bool IsEditableItemType(Type itemType)
        {
            ArgumentNullException.ThrowIfNull(itemType);
            return itemType.IsClass && itemType != typeof(string) && itemType != typeof(object);
        }

        /// <summary>
        /// Returns whether the items of <paramref name="collection"/> can be added, removed and reordered.
        /// </summary>
        internal static bool IsResizable(IList collection)
        {
            ArgumentNullException.ThrowIfNull(collection);
            return !collection.IsReadOnly && !collection.IsFixedSize;
        }

        /// <summary>
        /// Returns the values the drop-down of a <see cref="PropertyGridEditorKind.Choice"/> property offers.
        /// </summary>
        internal static IReadOnlyList<object> GetChoices(PropertyDescriptor property)
        {
            ArgumentNullException.ThrowIfNull(property);
            if (IsPlainEnum(property.PropertyType))
                return Enum.GetValues(property.PropertyType).Cast<object>().ToList();
            return property.Converter.GetStandardValues()?.Cast<object>().ToList() ?? [];
        }

        /// <summary>
        /// Returns whether the value of <paramref name="property"/> on <paramref name="component"/> is shown as changed
        /// (in bold).
        /// </summary>
        /// <remarks>
        /// <para>
        /// A collection counts as changed when it has items, which matches when the serializer writes it: the
        /// definition types skip an empty collection through a get-only <c>Specified</c> member. Their collection
        /// properties are get-only and created on first read, so <see cref="PropertyDescriptor.ShouldSerializeValue"/>
        /// is always true for them.
        /// </para>
        /// <para>
        /// Any other property counts as changed only when it can also be reset.
        /// <see cref="PropertyDescriptor.ShouldSerializeValue"/> is always true for a property with neither a
        /// <c>[DefaultValue]</c> nor a <c>ShouldSerialize</c> method of its own, so on its own it would put every such
        /// property in bold.
        /// </para>
        /// </remarks>
        internal static bool IsModified(PropertyDescriptor property, object component)
        {
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(component);
            if (IsCollectionType(property.PropertyType))
                return property.GetValue(component) is ICollection { Count: > 0 };
            return property.CanResetValue(component) && property.ShouldSerializeValue(component);
        }

        /// <summary>
        /// Returns whether the row of <paramref name="property"/> can be reset to its default value.
        /// </summary>
        internal static bool CanReset(PropertyDescriptor property, object component, bool gridReadOnly)
        {
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(component);
            return !gridReadOnly && !property.IsReadOnly && property.CanResetValue(component);
        }

        /// <summary>
        /// Returns the text a <see cref="PropertyGridEditorKind.Text"/> or summary row shows for <paramref name="value"/>.
        /// </summary>
        internal static string FormatText(PropertyDescriptor property, object? value)
        {
            ArgumentNullException.ThrowIfNull(property);
            if (value is null) { return string.Empty; }
            return property.Converter.CanConvertTo(typeof(string))
                ? property.Converter.ConvertToString(null, CultureInfo.CurrentCulture, value) ?? string.Empty
                : value.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Converts the text typed into a <see cref="PropertyGridEditorKind.Text"/> row into a value of the property's
        /// type.
        /// </summary>
        /// <param name="property">The property the text is for.</param>
        /// <param name="text">The typed text.</param>
        /// <param name="value">The converted value.</param>
        /// <param name="error">Why the text could not be converted; <c>null</c> on success.</param>
        /// <returns><c>true</c> when the text converted.</returns>
        internal static bool TryParseText(PropertyDescriptor property, string text, out object? value, out string? error)
        {
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(text);
            value = null;
            error = null;
            if (property.PropertyType == typeof(string))
            {
                value = text;
                return true;
            }
            try
            {
                value = property.Converter.ConvertFromString(null, CultureInfo.CurrentCulture, text);
                return true;
            }
            // NOTE: Converters report bad text with different exception types: the number converters wrap the parse
            // error in an ArgumentException, the enum converter throws FormatException.
            catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException
                or OverflowException or InvalidCastException)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Writes <paramref name="value"/> to <paramref name="property"/> on <paramref name="component"/>.
        /// </summary>
        /// <param name="property">The property to write.</param>
        /// <param name="component">The object that owns the property.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="error">The setter's reason when it rejected the value; <c>null</c> on success.</param>
        /// <returns><c>true</c> when the value was written.</returns>
        /// <remarks>
        /// A setter that validates its value, such as a key setter that refuses a key already in its collection,
        /// rejects it with an <see cref="ArgumentException"/> or an <see cref="InvalidOperationException"/>. Reflection
        /// wraps that exception, so the inner one supplies the message.
        /// </remarks>
        internal static bool TrySetValue(PropertyDescriptor property, object component, object? value, out string? error)
        {
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(component);
            error = null;
            try
            {
                property.SetValue(component, value);
                return true;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException or InvalidOperationException)
            {
                error = ex.InnerException!.Message;
                return false;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Writes <paramref name="property"/> back to its default value on <paramref name="component"/>.
        /// </summary>
        /// <param name="property">The property to reset.</param>
        /// <param name="component">The object that owns the property.</param>
        /// <param name="error">The setter's reason when it rejected the default value; <c>null</c> on success.</param>
        /// <returns><c>true</c> when the property was reset.</returns>
        internal static bool TryResetValue(PropertyDescriptor property, object component, out string? error)
        {
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(component);
            error = null;
            try
            {
                property.ResetValue(component);
                return true;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException or InvalidOperationException)
            {
                error = ex.InnerException!.Message;
                return false;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Returns the date a date picker shows for a <see cref="DateTime"/> or <see cref="DateOnly"/> value.
        /// </summary>
        /// <remarks>
        /// The date is built at offset zero from its parts. Converting the <see cref="DateTime"/> itself would apply
        /// the local offset, which throws for <see cref="DateTime.MinValue"/> east of UTC.
        /// </remarks>
        internal static DateTimeOffset? ToPickerDate(object? value) => value switch
        {
            DateTime dateTime => new DateTimeOffset(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0, TimeSpan.Zero),
            DateOnly date => new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero),
            _ => null,
        };

        /// <summary>
        /// Returns the value of type <paramref name="propertyType"/> for the date picked in a date picker.
        /// </summary>
        /// <param name="propertyType"><see cref="DateTime"/> or <see cref="DateOnly"/>.</param>
        /// <param name="picked">The picked date; only its date part is used.</param>
        /// <param name="original">The current value. A <see cref="DateTime"/> keeps its time of day and its kind.</param>
        internal static object FromPickerDate(Type propertyType, DateTimeOffset picked, object? original)
        {
            ArgumentNullException.ThrowIfNull(propertyType);
            if (propertyType == typeof(DateOnly))
                return new DateOnly(picked.Year, picked.Month, picked.Day);
            var time = original is DateTime old ? old.TimeOfDay : TimeSpan.Zero;
            var kind = original is DateTime previous ? previous.Kind : DateTimeKind.Unspecified;
            return new DateTime(picked.Year, picked.Month, picked.Day, 0, 0, 0, kind).Add(time);
        }

        /// <summary>
        /// Returns the value of <paramref name="propertyType"/> for a number from a numeric up-down, or <c>null</c> when
        /// the number does not fit the type.
        /// </summary>
        internal static object? FromNumber(Type propertyType, decimal number)
        {
            ArgumentNullException.ThrowIfNull(propertyType);
            try
            {
                return Convert.ChangeType(number, propertyType, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the range and format a numeric up-down uses for <paramref name="propertyType"/>.
        /// </summary>
        internal static (decimal Minimum, decimal Maximum, string? Format) GetNumericRange(Type propertyType)
        {
            ArgumentNullException.ThrowIfNull(propertyType);
            return Type.GetTypeCode(propertyType) switch
            {
                TypeCode.Byte => (byte.MinValue, byte.MaxValue, "0"),
                TypeCode.SByte => (sbyte.MinValue, sbyte.MaxValue, "0"),
                TypeCode.Int16 => (short.MinValue, short.MaxValue, "0"),
                TypeCode.UInt16 => (ushort.MinValue, ushort.MaxValue, "0"),
                TypeCode.Int32 => (int.MinValue, int.MaxValue, "0"),
                TypeCode.UInt32 => (uint.MinValue, uint.MaxValue, "0"),
                TypeCode.Int64 => (long.MinValue, long.MaxValue, "0"),
                TypeCode.UInt64 => (ulong.MinValue, ulong.MaxValue, "0"),
                _ => (decimal.MinValue, decimal.MaxValue, null),
            };
        }

        /// <summary>
        /// Returns <paramref name="text"/> through <paramref name="translator"/>, or as written when there is none or it
        /// returns <c>null</c> or an empty string.
        /// </summary>
        /// <param name="translator">The translator, or <c>null</c>.</param>
        /// <param name="kind">What the text is.</param>
        /// <param name="componentType">The type of the object the text belongs to.</param>
        /// <param name="propertyName">The name of the property the text belongs to, or <c>null</c>.</param>
        /// <param name="text">The text as written in the annotation.</param>
        internal static string Translate(Func<PropertyGridText, string?>? translator, PropertyGridTextKind kind,
            Type componentType, string? propertyName, string text)
        {
            if (translator is null || string.IsNullOrEmpty(text)) { return text; }
            var translated = translator(new PropertyGridText(kind, componentType, propertyName, text));
            return string.IsNullOrEmpty(translated) ? text : translated;
        }

        private static bool IsPlainEnum(Type type) => type.IsEnum && !type.IsDefined(typeof(FlagsAttribute), inherit: false);
    }
}
