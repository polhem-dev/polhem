using System.ComponentModel;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// An object with one property of each shape <c>PropertyGridControl</c> treats differently.
    /// </summary>
    public sealed class PropertyGridSample
    {
        private string _guarded = string.Empty;

        [Category("General")]
        [Description("The sample's name.")]
        [DefaultValue("")]
        public string Name { get; set; } = string.Empty;

        [Category("General")]
        [DefaultValue(false)]
        public bool Enabled { get; set; }

        [Category("Layout")]
        [DefaultValue(SampleMode.First)]
        public SampleMode Mode { get; set; }

        [DefaultValue(0)]
        public int Count { get; set; }

        [Category("General")]
        public string NoDefault { get; set; } = "x";

        [Browsable(false)]
        public string Hidden { get; set; } = string.Empty;

        [Category("Layout")]
        public DateTime When { get; set; }

        [Category("Layout")]
        public List<string> Items { get; } = [];

        [Category("Layout")]
        public ChildSettings Child { get; set; } = new();

        [Category("Layout")]
        public SampleOptions Flags { get; set; }

        [Category("General")]
        public string Computed => Name + "!";

        [Category("General")]
        [DefaultValue("")]
        public string Guarded
        {
            get { return _guarded; }
            set
            {
                if (value == "bad")
                    throw new ArgumentException("The value is refused.", nameof(value));
                _guarded = value;
            }
        }

        /// <summary>
        /// A plain enum for <see cref="PropertyGridSample"/>.
        /// </summary>
        public enum SampleMode
        {
            First,
            Second,
        }

        /// <summary>
        /// A flags enum for <see cref="PropertyGridSample"/>.
        /// </summary>
        [Flags]
        public enum SampleOptions
        {
            None = 0,
            A = 1,
            B = 2,
        }

        /// <summary>
        /// A nested object behind an <see cref="ExpandableObjectConverter"/>, as the settings types use.
        /// </summary>
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public sealed class ChildSettings
        {
            public string Value { get; set; } = string.Empty;

            public override string ToString() => "child";
        }
    }
}
