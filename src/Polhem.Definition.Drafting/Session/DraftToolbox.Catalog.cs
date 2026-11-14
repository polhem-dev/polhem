using System.ComponentModel;
using System.Globalization;

namespace Polhem.Definition.Drafting.Session
{
    /// <summary>
    /// The tools that look at what exists outside the draft: the host's catalog of forms, the relations to them, and the
    /// host's validation that decides whether the draft is ready.
    /// </summary>
    internal sealed partial class DraftToolbox
    {
        private const int MaxSearchResults = 20;

        private object SearchProgIds(
            [Description("Keywords separated by spaces, such as 'employee 員工'. A form matches when any keyword is part of its ProgId or names.")] string query)
        {
            var terms = query.Split([' ', ',', ';', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (terms.Length == 0)
                throw new DraftToolException("Pass at least one keyword.");
            var matches = _host.GetCatalog()
                .Where(entry => terms.Any(term => Contains(entry.ProgId, term) || Contains(entry.DisplayName, term)
                    || Contains(entry.DisplayNameZhTw, term)))
                .Take(MaxSearchResults)
                .Select(entry => new
                {
                    progId = entry.ProgId,
                    displayName = entry.DisplayName,
                    displayNameZhTw = entry.DisplayNameZhTw,
                    fields = entry.Fields.Select(field => field.FieldName).ToList(),
                })
                .ToList();
            return matches.Count > 0 ? matches : "No existing form matches. Try other keywords, or leave the field without a relation.";
        }

        private object GetProgIdFields(
            [Description("The ProgId of an existing form.")] string progId)
        {
            var entry = FindCatalogEntry(progId) ?? throw new DraftToolException(NoSuchForm(progId));
            var fields = _host.GetFields(entry.ProgId);
            return fields.Select(field => new { fieldName = field.FieldName, caption = field.Caption, dbType = field.DbType.ToString() })
                .ToList();
        }

        private string SetRelation(
            [Description($"'{MasterTableKey}' or the name of a detail table.")] string table,
            [Description("The field name.")] string fieldName,
            [Description("The ProgId of the existing form, as search_progids returns it. Empty removes the relation.")] string progId,
            [Description("How sure you are of the match, from 0 to 1. Use 1 once the user has confirmed it.")] double confidence,
            [Description("Why this form matches, shown to the user.")] string reason)
        {
            var (_, field) = FindField(table, fieldName);
            if (progId.Trim().Length == 0)
            {
                field.Relation = null;
                DraftModified = true;
                return $"Removed the relation of '{field.FieldName}'.";
            }
            var entry = FindCatalogEntry(progId) ?? throw new DraftToolException(NoSuchForm(progId));
            if (double.IsNaN(confidence) || confidence < 0 || confidence > 1)
                throw new DraftToolException($"The confidence must be between 0 and 1, not {confidence.ToString(CultureInfo.InvariantCulture)}.");

            field.Relation = new DraftRelation { ProgId = entry.ProgId, Confidence = confidence, Reason = reason.Trim() };
            DraftModified = true;
            var message = $"'{field.FieldName}' now picks a record from '{entry.ProgId}'.";
            if (confidence < _relationConfirmationThreshold)
                message += " The confidence is low: ask the user to confirm this match, and once they do, call set_relation "
                    + "again with confidence 1. The draft cannot be marked ready before that.";
            return message;
        }

        private object ValidateDraft()
        {
            var issues = CollectIssues();
            return new
            {
                errors = issues.Count(issue => issue.Severity == DraftIssueSeverity.Error),
                warnings = issues.Count(issue => issue.Severity == DraftIssueSeverity.Warning),
                issues = issues.Select(issue => new { severity = issue.Severity.ToString(), location = issue.Location, message = issue.Message })
                    .ToList(),
            };
        }

        private string MarkReady()
        {
            var errors = CollectIssues().Where(issue => issue.Severity == DraftIssueSeverity.Error).ToList();
            if (errors.Count > 0)
                throw new DraftToolException("The draft is not ready. Fix these errors first, asking the user where needed:\n"
                    + string.Join('\n', errors));
            IsReady = true;
            return "The draft is ready to import. Tell the user they can now create the form.";
        }

        /// <summary>
        /// Collects what <c>mark_ready</c> checks: the converter's findings, the host's validation, and what only the
        /// session knows (an undecided ProgId the converter previews under a placeholder, unconfirmed relations).
        /// </summary>
        private List<DraftIssue> CollectIssues()
        {
            var result = FormDraftConverter.Convert(_draft);
            var issues = new List<DraftIssue>(result.Issues);
            if (_draft.ProgId.Trim().Length == 0)
                issues.Add(new DraftIssue(DraftIssueSeverity.Error, "Schema", "The ProgId is not decided yet."));
            if (_draft.Tables.Count == 0 || _draft.Tables[0].Fields.Count == 0)
                issues.Add(new DraftIssue(DraftIssueSeverity.Error, MasterTableKey, "The master table has no fields."));
            foreach (var table in _draft.Tables)
            {
                foreach (var field in table.Fields.Where(f => f.Relation is { } relation
                    && relation.ProgId.Length > 0 && relation.Confidence < _relationConfirmationThreshold))
                {
                    issues.Add(new DraftIssue(DraftIssueSeverity.Error, $"{DescribeTable(table)}.{field.FieldName}",
                        $"The relation to '{field.Relation!.ProgId}' has low confidence and is not confirmed by the user."));
                }
            }
            issues.AddRange(_host.Validate(result));
            return issues;
        }

        private DraftCatalogEntry? FindCatalogEntry(string progId)
        {
            var trimmed = progId.Trim();
            return _host.GetCatalog().FirstOrDefault(entry => string.Equals(entry.ProgId, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        private static string NoSuchForm(string progId)
            => $"There is no existing form '{progId}'. Use search_progids to find one.";

        private static bool Contains(string text, string term) => text.Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
