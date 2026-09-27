using System.Data;
using Polhem.Base.Exceptions;
using Polhem.Definition.Filters;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Permission-gate half of FormBusinessObject: the layer-1 action gate and the read-side record
    /// scope filter. The save path's record-scope checks are in <c>FormBusinessObject.WriteScope.cs</c>.
    /// </summary>
    public partial class FormBusinessObject
    {
        #region Authorization (layer-1 model + action gate)

        // The order in which write actions are evaluated. Layer one does not consider record
        // scope, so one check per action is enough — checking it per row would give the same answer.
        private static readonly PermissionActions[] s_writeActions =
            { PermissionActions.Create, PermissionActions.Update, PermissionActions.Delete };

        /// <summary>
        /// Enforces the layer-1 permission check for <paramref name="action"/> on this form's
        /// permission model. A no-op when the FormSchema declares no <c>PermissionModelId</c>
        /// (gradual adoption — unmarked forms stay open). Throws when the caller lacks the grant.
        /// </summary>
        /// <param name="action">The single <see cref="PermissionActions"/> flag to require.</param>
        /// <exception cref="ForbiddenException">The caller is not granted the action.</exception>
        private void Authorize(PermissionActions action)
        {
            var modelId = DefineAccess.GetFormSchema(ProgId).PermissionModelId;
            if (string.IsNullOrEmpty(modelId)) { return; }

            var authorization = Services.GetRequiredService<ICompanyAuthorizationService>();
            if (!authorization.Can(AccessToken, modelId, action))
                throw new ForbiddenException($"Permission denied: '{action}' on model '{modelId}'.");
        }

        /// <summary>
        /// Resolves the layer-2 record-scope read filter for <paramref name="action"/> on this form's
        /// permission model. Returns <c>null</c> when the FormSchema declares no <c>PermissionModelId</c>
        /// (unscoped form) or the effective scope is unrestricted — in both cases no filter is applied.
        /// </summary>
        /// <param name="action">The action whose read scope is resolved (typically <c>Read</c>).</param>
        private FilterNode? ResolveScopeFilter(PermissionActions action)
        {
            var schema = DefineAccess.GetFormSchema(ProgId);
            if (string.IsNullOrEmpty(schema.PermissionModelId)) { return null; }

            var resolver = Services.GetRequiredService<IScopeResolver>();
            return resolver.ResolveFilter(AccessToken, schema.PermissionModelId, action, schema);
        }

        /// <summary>
        /// AND-combines the caller-supplied list filter with the record-scope filter (either may be <c>null</c>).
        /// </summary>
        private static FilterNode? CombineWithScope(FilterNode? clientFilter, FilterNode? scopeFilter)
        {
            if (scopeFilter == null) { return clientFilter; }
            if (clientFilter == null) { return scopeFilter; }
            return FilterGroup.All(scopeFilter, clientFilter);
        }

        /// <summary>
        /// Enforces the layer-1 permission check for a Save by deriving the required actions
        /// from each row's <c>RowState</c> (Added→Create / Modified→Update / Deleted→Delete)
        /// and verifying every distinct action present in the DataSet.
        /// </summary>
        /// <param name="dataSet">The DataSet about to be persisted.</param>
        /// <exception cref="ForbiddenException">The caller lacks one of the required actions.</exception>
        private void AuthorizeSave(DataSet dataSet)
        {
            var modelId = DefineAccess.GetFormSchema(ProgId).PermissionModelId;
            if (string.IsNullOrEmpty(modelId)) { return; }

            var required = CollectRowStateActions(dataSet);
            if (required == PermissionActions.None) { return; }

            var authorization = Services.GetRequiredService<ICompanyAuthorizationService>();

            // s_writeActions holds only non-zero flags, so None is a safe "no denial" sentinel.
            var denied = s_writeActions.FirstOrDefault(
                action => required.HasFlag(action) && !authorization.Can(AccessToken, modelId, action));
            if (denied != PermissionActions.None)
                throw new ForbiddenException($"Permission denied: '{denied}' on model '{modelId}'.");
        }

        /// <summary>
        /// OR-merges the <see cref="PermissionActions"/> implied by every row's <c>RowState</c>
        /// across all tables in the DataSet.
        /// </summary>
        private static PermissionActions CollectRowStateActions(DataSet dataSet)
        {
            var actions = PermissionActions.None;
            foreach (DataTable table in dataSet.Tables)
            {
                foreach (DataRow row in table.Rows)
                {
                    actions |= row.RowState switch
                    {
                        DataRowState.Added => PermissionActions.Create,
                        DataRowState.Modified => PermissionActions.Update,
                        DataRowState.Deleted => PermissionActions.Delete,
                        _ => PermissionActions.None,
                    };
                }
            }
            return actions;
        }

        #endregion
    }
}
