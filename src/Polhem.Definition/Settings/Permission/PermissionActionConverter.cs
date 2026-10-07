using System.ComponentModel;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// Converts a <see cref="PermissionActions"/> value that holds exactly one action, and offers the single actions as
    /// its exclusive standard values.
    /// </summary>
    /// <remarks>
    /// <see cref="PermissionRule.Action"/> is the key of its rule and names one action, yet <see cref="PermissionActions"/>
    /// is a flags enum, whose default converter lets an editor combine members. This converter is what makes a property
    /// grid offer a drop-down of single actions instead. <c>XmlSerializer</c> does not read type converters, so the
    /// serialized form is unchanged (<c>PermissionModelsDataTests.PermissionRule_EachAction_RoundTripsThroughXmlByName</c> pins it).
    /// </remarks>
    internal sealed class PermissionActionConverter : EnumConverter
    {
        private static readonly PermissionActions[] s_actions =
        [
            PermissionActions.Create,
            PermissionActions.Read,
            PermissionActions.Update,
            PermissionActions.Delete,
            PermissionActions.Print,
            PermissionActions.Export,
        ];

        /// <summary>
        /// Initializes a new instance of <see cref="PermissionActionConverter"/>.
        /// </summary>
        public PermissionActionConverter()
            : base(typeof(PermissionActions))
        { }

        /// <inheritdoc/>
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
            => new(s_actions);
    }
}
