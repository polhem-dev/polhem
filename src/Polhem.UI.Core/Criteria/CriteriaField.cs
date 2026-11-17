using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;

namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// A field a user can add a condition row for, described independently of where it comes from: a form's
    /// <see cref="FormField"/> through <see cref="FromField"/>, or a report's own parameter.
    /// </summary>
    public sealed class CriteriaField
    {
        private static readonly CriteriaOperator[] s_textOperators = [CriteriaOperator.Contains, CriteriaOperator.StartsWith, CriteriaOperator.Equal];
        private static readonly CriteriaOperator[] s_codeOperators = [CriteriaOperator.Range, CriteriaOperator.StartsWith, CriteriaOperator.Contains, CriteriaOperator.Equal];
        private static readonly CriteriaOperator[] s_inOperators = [CriteriaOperator.In];
        private static readonly CriteriaOperator[] s_booleanOperators = [CriteriaOperator.Equal];
        private static readonly CriteriaOperator[] s_rangeOperators = [CriteriaOperator.Range, CriteriaOperator.Equal];

        /// <summary>
        /// Initializes a new instance of <see cref="CriteriaField"/>.
        /// </summary>
        /// <param name="fieldName">The field name a backend filter compares.</param>
        /// <param name="caption">The caption shown to the user.</param>
        /// <param name="kind">What the user enters for it.</param>
        /// <param name="dbType">The stored type, which the values of a <see cref="CriteriaFieldKind.Choice"/> or
        /// <see cref="CriteriaFieldKind.Relation"/> field are converted to.</param>
        /// <param name="operators">The operators offered, the first being the default; <c>null</c> offers every
        /// operator the kind supports.</param>
        /// <exception cref="ArgumentException">An operator is one the kind does not support, or the list is empty.</exception>
        public CriteriaField(string fieldName, string caption, CriteriaFieldKind kind, FieldDbType dbType,
            IEnumerable<CriteriaOperator>? operators = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
            FieldName = fieldName;
            Caption = string.IsNullOrEmpty(caption) ? fieldName : caption;
            Kind = kind;
            DbType = dbType;
            var supported = SupportedOperators(kind);
            if (operators is null)
            {
                Operators = Array.AsReadOnly(supported);
                return;
            }
            var chosen = operators.Distinct().ToArray();
            if (chosen.Length == 0)
                throw new ArgumentException("A field must offer at least one operator.", nameof(operators));
            var unsupported = chosen.Where(o => !supported.Contains(o)).ToArray();
            if (unsupported.Length > 0)
                throw new ArgumentException($"A {kind} field does not support {string.Join(", ", unsupported)}.", nameof(operators));
            Operators = Array.AsReadOnly(chosen);
        }

        /// <summary>Gets the field name a backend filter compares.</summary>
        public string FieldName { get; }

        /// <summary>Gets the caption shown to the user.</summary>
        public string Caption { get; }

        /// <summary>Gets what the user enters for the field.</summary>
        public CriteriaFieldKind Kind { get; }

        /// <summary>Gets the stored type of the field.</summary>
        public FieldDbType DbType { get; }

        /// <summary>Gets the operators offered, the first being the default.</summary>
        public IReadOnlyList<CriteriaOperator> Operators { get; }

        /// <summary>Gets the operator a new condition row starts with.</summary>
        public CriteriaOperator DefaultOperator => Operators[0];

        /// <summary>Gets the program a <see cref="CriteriaFieldKind.Relation"/> field picks its rows from; empty otherwise.</summary>
        public string RelationProgId { get; init; } = string.Empty;

        /// <summary>Gets the values a <see cref="CriteriaFieldKind.Choice"/> field lists, when the definition lists them itself.</summary>
        public IReadOnlyList<ListItem> ListItems { get; init; } = [];

        /// <summary>Gets the language enum a <see cref="CriteriaFieldKind.Choice"/> field takes its values from; empty otherwise.</summary>
        public string LangEnumName { get; init; } = string.Empty;

        /// <summary>
        /// Describes a form field, or returns <c>null</c> for a field no condition can be entered for
        /// (long text, binary data, a time of day, or a <see cref="Guid"/> that names no related program).
        /// </summary>
        /// <param name="field">The form field.</param>
        /// <returns>The description, or <c>null</c>.</returns>
        /// <remarks>
        /// A string field is a <see cref="CriteriaFieldKind.Code"/> when it is named <c>sys_id</c> or ends with
        /// <c>_id</c> (the document number, or a related row's code such as <c>ref_customer_id</c>), and
        /// <see cref="CriteriaFieldKind.Text"/> otherwise.
        /// </remarks>
        public static CriteriaField? FromField(FormField field)
        {
            ArgumentNullException.ThrowIfNull(field);
            var kind = ResolveKind(field);
            if (kind is null) return null;
            return new CriteriaField(field.FieldName, field.Caption, kind.Value, field.DbType)
            {
                RelationProgId = kind == CriteriaFieldKind.Relation ? field.RelationProgId : string.Empty,
                ListItems = kind == CriteriaFieldKind.Choice && field.ListItems is { } items ? [.. items] : [],
                LangEnumName = kind == CriteriaFieldKind.Choice ? field.LangEnumName : string.Empty,
            };
        }

        private static CriteriaFieldKind? ResolveKind(FormField field)
        {
            if (!string.IsNullOrEmpty(field.RelationProgId)) return CriteriaFieldKind.Relation;
            if (field.ListItems is { Count: > 0 } || !string.IsNullOrEmpty(field.LangEnumName)) return CriteriaFieldKind.Choice;
            return field.DbType switch
            {
                FieldDbType.String => IsCodeName(field.FieldName) ? CriteriaFieldKind.Code : CriteriaFieldKind.Text,
                FieldDbType.Boolean => CriteriaFieldKind.Boolean,
                FieldDbType.AutoIncrement or FieldDbType.Short or FieldDbType.Integer or FieldDbType.Long
                    or FieldDbType.Decimal or FieldDbType.Currency => CriteriaFieldKind.Number,
                FieldDbType.Date => CriteriaFieldKind.Date,
                FieldDbType.DateTime => CriteriaFieldKind.DateTime,
                _ => null,
            };
        }

        private static bool IsCodeName(string fieldName)
            => fieldName.Equals("sys_id", StringComparison.OrdinalIgnoreCase)
                || fieldName.EndsWith("_id", StringComparison.OrdinalIgnoreCase);

        private static CriteriaOperator[] SupportedOperators(CriteriaFieldKind kind) => kind switch
        {
            CriteriaFieldKind.Text => s_textOperators,
            CriteriaFieldKind.Code => s_codeOperators,
            CriteriaFieldKind.Relation or CriteriaFieldKind.Choice => s_inOperators,
            CriteriaFieldKind.Boolean => s_booleanOperators,
            CriteriaFieldKind.Number or CriteriaFieldKind.Date or CriteriaFieldKind.DateTime => s_rangeOperators,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown criteria field kind."),
        };
    }
}
