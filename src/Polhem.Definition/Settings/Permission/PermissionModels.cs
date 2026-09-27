using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using Polhem.Base.Attributes;
using Polhem.Base.Serialization;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// The permission model registry: every authorizable target model with its supported
    /// actions and default record-scope strategies. A global singleton definition (one file
    /// per deployment), loaded as the master list of authorizable targets for role authorization.
    /// </summary>
    [Description("Permission model registry.")]
    [TreeNode("Permission Models")]
    public class PermissionModels : IObjectSerializeFile
    {
        private PermissionModelCollection? _models = null;

        #region IObjectSerializeFile Interface

        /// <summary>
        /// Gets the file path bound to serialization.
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        [Browsable(false)]
        public string ObjectFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the file path bound for serialization/deserialization.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public void SetObjectFilePath(string filePath)
        {
            ObjectFilePath = filePath;
        }

        #endregion

        /// <summary>
        /// Gets the permission model collection.
        /// </summary>
        [Description("Permission model collection.")]
        [DefaultValue(null)]
        public PermissionModelCollection? Models
        {
            get
            {
                if (_models == null) { _models = new PermissionModelCollection(this); }
                return _models;
            }
        }

        /// <summary>
        /// Gets whether <see cref="Models"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Base.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ModelsSpecified => _models is { Count: > 0 };

        /// <summary>
        /// Validates the registry and returns one message per violation (empty when valid).
        /// Enforces that egress actions (Print / Export) do not define an explicit scope —
        /// their output range always equals the model's Read scope, so a separate scope would
        /// only drift.
        /// </summary>
        /// <returns>The list of validation errors; empty when the registry is valid.</returns>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            var models = Models;
            if (models == null) { return errors; }

            foreach (var model in models)
            {
                var rules = model.Rules;
                if (rules == null) { continue; }
                foreach (var rule in rules)
                {
                    bool isEgress = rule.Action is PermissionActions.Print or PermissionActions.Export;
                    if (isEgress && rule.Scope != ScopeStrategy.Inherit)
                    {
                        errors.Add($"Model '{model.ModelId}': egress action '{rule.Action}' must not define a scope; it inherits Read.");
                    }
                }
            }
            return errors;
        }
    }
}
