using System.Data;
using System.Globalization;
using Polhem.Core;
using Polhem.Definition;
using Polhem.Definition.Attributes;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Paging;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;

namespace Polhem.Business.Form
{
    public partial class FormBusinessObject
    {
        /// <summary>
        /// Called by the UI when the user changes the value of a field marked
        /// <see cref="FormField.ValueChangeCallback"/>; returns the values to write into other fields of the edited
        /// row and the messages to show on its fields.
        /// </summary>
        /// <param name="args">The input arguments.</param>
        /// <remarks>
        /// <para>
        /// The caller must hold <c>Create</c> or <c>Update</c> on the form's permission model, since it is editing
        /// the form. The table and field must exist in the form's schema.
        /// </para>
        /// <para>
        /// For a marked field with a <see cref="FormField.RelationProgId"/> and mappings,
        /// <see cref="ResolveRelationValues"/> fills the destination of every mapping from the related form. A field
        /// that is not marked gets an empty result. Override this method for anything else, such as validating a
        /// value or bringing a part number's stock and price: call the base first, then add to its result, branching
        /// on <see cref="FieldValueChangeArgs.FieldName"/>.
        /// </para>
        /// <para>
        /// The values in the result never call back: the framework does not invoke this method for the fields it
        /// returns, and the client writes them without calling back either. Compute everything one change needs in
        /// the same call instead of calling this method again for another field.
        /// </para>
        /// </remarks>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public virtual FieldValueChangeResult FieldValueChange(FieldValueChangeArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            AuthorizeAny(PermissionActions.Create, PermissionActions.Update);

            var field = DefineAccess.GetFormSchema(ProgId).Tables?.GetOrDefault(args.TableName)?.Fields?.GetOrDefault(args.FieldName)
                ?? throw new ArgumentException(
                    $"Form '{ProgId}' has no field '{args.FieldName}' in table '{args.TableName}'.", nameof(args));

            var result = new FieldValueChangeResult();
            if (field.ValueChangeCallback && StringUtilities.IsNotEmpty(field.RelationProgId)
                && field.RelationFieldMappings is { Count: > 0 })
            {
                ResolveRelationValues(field, args, result);
            }
            return result;
        }

        /// <summary>
        /// Fills <paramref name="result"/> with the destination value of every mapping of the relation field
        /// <paramref name="field"/>, read from the row of the related form that <see cref="FieldValueChangeArgs.Value"/>
        /// names.
        /// </summary>
        /// <param name="field">The relation field that changed.</param>
        /// <param name="args">The arguments of the call.</param>
        /// <param name="result">The result to fill.</param>
        /// <remarks>
        /// An empty value clears every destination. The related row is read by the related form's own business
        /// object through <see cref="ResolveRelationRow"/>, under the rules its lookup applies, so a value the caller
        /// could not pick in the lookup brings nothing back: every destination is cleared and the field gets an error.
        /// </remarks>
        protected virtual void ResolveRelationValues(FormField field, FieldValueChangeArgs args, FieldValueChangeResult result)
        {
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(result);

            var mappings = field.RelationFieldMappings!;
            DataRow? row = null;
            if (StringUtilities.IsNotEmpty(args.Value))
            {
                if (Guid.TryParse(args.Value, out var rowId)
                    && BoFactory.CreateBusinessObject(AccessToken, field.RelationProgId, IsLocalCall) is FormBusinessObject related)
                {
                    row = related.ResolveRelationRow(rowId, mappings.Select(m => m.SourceField));
                }
                if (row is null)
                {
                    var displayName = DefineAccess.GetFormSchema(field.RelationProgId).DisplayName;
                    result.Errors[field.FieldName] = string.Format(CultureInfo.CurrentCulture,
                        ResolveText(PolhemMessages.FieldValueChangeRelationNotFound, "No {0} record that can be picked matches this value."),
                        StringUtilities.IsNotEmpty(displayName) ? displayName : field.RelationProgId);
                }
            }

            foreach (var mapping in mappings)
            {
                var column = row?.Table.Columns[mapping.SourceField];
                result.SetValue(mapping.DestinationField, column is null ? null : row![column], column?.DataType);
            }
        }

        /// <summary>
        /// Reads the master row <paramref name="rowId"/> of this form with the fields <paramref name="fieldNames"/>,
        /// under the rules <see cref="GetLookup"/> applies; <c>null</c> when no such row may be picked.
        /// </summary>
        /// <param name="rowId">The <c>sys_rowid</c> of the row.</param>
        /// <param name="fieldNames">The fields to read; names this form's master table does not have are skipped.</param>
        /// <remarks>
        /// Another form's <see cref="ResolveRelationValues"/> calls it on this form's business object. Like
        /// <see cref="GetLookup"/> it does not require this form's <c>Read</c> permission, but it applies
        /// <see cref="GetLookupFilter"/> and, when <see cref="LookupAppliesRecordScope"/> is set, the caller's
        /// <c>Read</c> record scope. It is not an API action.
        /// </remarks>
        protected internal virtual DataRow? ResolveRelationRow(Guid rowId, IEnumerable<string> fieldNames)
        {
            ArgumentNullException.ThrowIfNull(fieldNames);
            var master = DefineAccess.GetFormSchema(ProgId).MasterTable;
            if (master?.Fields is null) { return null; }

            var names = fieldNames
                .Where(name => master.Fields.Contains(name) && !StringUtilities.IsEquals(name, SysFields.RowId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Prepend(SysFields.RowId);
            var filter = CombineWithScope(
                CombineWithScope(FilterCondition.Equal(SysFields.RowId, rowId), GetLookupFilter()),
                LookupAppliesRecordScope ? ResolveScopeFilter(PermissionActions.Read) : null);

            var table = CreateDataFormRepository(ProgId)
                .GetList(string.Join(",", names), filter, null, new PagingOptions { PageSize = 1 }).Table;
            return table is { Rows.Count: > 0 } ? table.Rows[0] : null;
        }

        /// <summary>
        /// Resolves <paramref name="fullKey"/> in the session's language, or returns <paramref name="englishText"/>
        /// when no culture of the chain declares it.
        /// </summary>
        private string ResolveText(string fullKey, string englishText)
        {
            int dot = fullKey.IndexOf('.');
            return LanguageService.TryResolveLangText(GetCurrentCustomizeId(), GetCurrentLang(),
                fullKey[..dot], fullKey[(dot + 1)..], out var text)
                ? text
                : englishText;
        }
    }
}
