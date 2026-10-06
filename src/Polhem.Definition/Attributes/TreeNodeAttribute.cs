using System.ComponentModel;
using Polhem.Core;

namespace Polhem.Definition.Attributes
{
    /// <summary>
    /// Custom attribute applied to a class to describe how the object is presented as a tree node.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TreeNodeAttribute : Attribute
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="TreeNodeAttribute"/>.
        /// </summary>
        public TreeNodeAttribute()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="TreeNodeAttribute"/> with a display format string.
        /// </summary>
        /// <param name="displayFormat">The display name or format string.</param>
        public TreeNodeAttribute(string displayFormat)
        {
            DisplayFormat = displayFormat;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TreeNodeAttribute"/> with a format string and a property name.
        /// </summary>
        /// <param name="displayFormat">The display format string.</param>
        /// <param name="propertyName">The property name used to replace <c>{0}</c> in the format string.</param>
        public TreeNodeAttribute(string displayFormat, string propertyName)
        {
            DisplayFormat = displayFormat;
            PropertyName = propertyName;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TreeNodeAttribute"/> with a display format and a collection folder flag.
        /// </summary>
        /// <param name="displayFormat">The display name or format string.</param>
        /// <param name="collectionFolder">Whether to show a folder node for collection properties.</param>
        public TreeNodeAttribute(string displayFormat, bool collectionFolder)
        {
            DisplayFormat = displayFormat;
            CollectionFolder = collectionFolder;
        }

        #endregion

        /// <summary>
        /// Gets the display format string.
        /// </summary>
        public string DisplayFormat { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the property name used to replace <c>{0}</c> in the format string.
        /// </summary>
        public string PropertyName { get; private set; } = string.Empty ;

        /// <summary>
        /// Gets whether collection properties should display a folder node.
        /// </summary>
        public bool CollectionFolder { get; private set; } = false;

        /// <summary>
        /// Gets the display text for the object that has <see cref="TreeNodeAttribute"/> applied.
        /// </summary>
        /// <param name="value">The object instance.</param>
        public static string GetDisplayText(object value)
        {
            return GetDisplayText(value, null);
        }

        /// <summary>
        /// Gets the display text for the object that has <see cref="TreeNodeAttribute"/> applied, translating
        /// <see cref="DisplayFormat"/> before it is used.
        /// </summary>
        /// <param name="value">The object instance.</param>
        /// <param name="formatTranslator">
        /// Translates <see cref="DisplayFormat"/>, a literal label or a composite format string, before the property
        /// values are put into it; <c>null</c> uses it as written.
        /// </param>
        /// <remarks>
        /// Only the format is translated, never the property values or the fallback text from
        /// <see cref="IDisplayName"/> or <see cref="object.ToString"/>.
        /// </remarks>
        public static string GetDisplayText(object value, Func<string, string>? formatTranslator)
        {
            var attribute = TypeDescriptor.GetAttributes(value)[typeof(TreeNodeAttribute)] as TreeNodeAttribute;
            if (attribute == null) { return value.ToString() ?? string.Empty; }

            var format = attribute.DisplayFormat;
            if (formatTranslator != null && StringUtilities.IsNotEmpty(format))
                format = formatTranslator(format);

            string displayText;
            if (StringUtilities.IsNotEmpty(attribute.PropertyName))
            {
                // DisplayFormat is a composite format string
                var names = StringUtilities.Split(attribute.PropertyName, ",");
                var args = new object[names.Length];
                for (int N1 = 0; N1 < names.Length; N1++)
                    args[N1] = TypeDescriptor.GetProperties(value)[names[N1]]?.GetValue(value)!;
                displayText = StringUtilities.Format(format, args);
            }
            else
            {
                // DisplayFormat is a literal string
                displayText = format;
            }

            if (StringUtilities.IsEmpty(displayText))
            {
                if (value is IDisplayName displayName)
                    displayText = displayName.DisplayName ?? string.Empty;
                else
                    displayText = value.ToString() ?? string.Empty;
            }

            return displayText;
        }

    }
}
