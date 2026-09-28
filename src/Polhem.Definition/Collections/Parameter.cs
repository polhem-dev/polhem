using System.ComponentModel;
using Polhem.Base.Collections;
using Polhem.Base;

namespace Polhem.Definition.Collections
{
    /// <summary>
    /// A parameter item.
    /// </summary>
    [DefaultProperty("Value")]
    public sealed class Parameter : KeyCollectionItem
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="Parameter"/>.
        /// </summary>
        public Parameter()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="Parameter"/>.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        public Parameter(string name, object value)
        {
            this.Name = name;
            Value = value;
        }

        #endregion

        /// <summary>
        /// Gets or sets the parameter name.
        /// </summary>
        public string Name
        {
            get { return base.Key; }
            set { base.Key = value; }
        }

        /// <summary>
        /// Gets or sets the parameter value.
        /// </summary>
        /// <remarks>
        /// Holds a framework value type (a string, a number, a <see cref="bool"/>, a <see cref="Guid"/>, a date or
        /// time, a <see cref="byte"/> array) or a nested <see cref="ParameterCollection"/>. A value of an application-defined type travels only
        /// through the named-type escape hatch (a namespace added to <see cref="Polhem.Base.SysInfo.AllowedTypeNamespaces"/>),
        /// and on iOS over the MessagePack codec it fails with a <see cref="NotSupportedException"/> that names
        /// the type. The details are under "Code generation at run time (iOS)" in
        /// <c>docs/en/platform-support.md</c>.
        /// </remarks>
        public object? Value { get; set; } = null;

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return StringUtilities.Format("{0}={1}", this.Name, this.Value!);
        }
    }
}
