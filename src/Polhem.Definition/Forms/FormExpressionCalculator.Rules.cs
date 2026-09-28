using System.Data;
using System.Globalization;
using Polhem.Base;
using Polhem.Base.Exceptions;

namespace Polhem.Definition.Forms
{
    public sealed partial class FormExpressionCalculator
    {
        /// <summary>
        /// Evaluates the enabled rules of the given trigger in order; a failing condition (that passes
        /// its applicability guard) aborts the operation with the rule's message. This is the server's
        /// before-save / before-delete validation pass; clients do not call it (the server is the
        /// authority for validation).
        /// </summary>
        /// <param name="schema">The form schema.</param>
        /// <param name="dataSet">The data set to validate.</param>
        /// <param name="trigger">The rule trigger to evaluate.</param>
        /// <exception cref="UserMessageException">A rule's condition fails; carries the rule message.</exception>
        /// <param name="timeZoneId">The user's IANA time zone id, seen by the <c>Today()</c> helper; blank means UTC.</param>
        /// <remarks>
        /// <c>Now()</c> is evaluated on the <see cref="DateTimeBasis.Utc"/> basis, for the same reason as
        /// <see cref="ApplyFieldExpressions"/>: a rule comparing a cell with <c>Now()</c> must compare two
        /// UTC values.
        /// </remarks>
        public void ValidateRules(FormSchema schema, DataSet dataSet, FormRuleTrigger trigger, string timeZoneId = "")
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(dataSet);

            if (schema.Rules == null) { return; }

            var rules = schema.Rules
                .Where(r => r.Enabled && r.Trigger == trigger)
                .OrderBy(r => r.Order)
                .ToList();

            foreach (var rule in rules)
            {
                var formTable = ResolveRuleTable(rule, schema);
                if (formTable == null) { continue; }
                var dataTable = FindDataTable(dataSet, formTable.TableName);
                if (dataTable == null) { continue; }
                ValidateRuleRows(rule, schema.ProgId, formTable, dataTable, timeZoneId, DateTimeBasis.Utc);
            }
        }

        /// <summary>
        /// Evaluates a single rule against every live row of its table; a row that passes the rule's
        /// applicability guard (<see cref="FormRule.When"/>) but fails its condition aborts with the message.
        /// </summary>
        /// <exception cref="UserMessageException">
        /// A row's condition fails. Carries the rule message as its English text and, when the rule
        /// and the schema are both named, the language key <c>{progId}.Rule.{RuleId}.Message</c>
        /// (<see cref="Language.FormSchemaLocalizer.RuleMessageKeyFormat"/>), which the server
        /// resolves in the session's culture before the message reaches the user.
        /// </exception>
        private void ValidateRuleRows(FormRule rule, string progId, FormTable formTable, DataTable dataTable, string timeZoneId,
            DateTimeBasis basis)
        {
            foreach (DataRow row in dataTable.Rows)
            {
                if (row.RowState is DataRowState.Deleted or DataRowState.Detached) { continue; }

                var variables = BuildVariables(row, formTable);
                if (StringUtilities.IsNotEmpty(rule.When) &&
                    !_evaluator.Evaluate<bool>(rule.When, NarrowVariables(rule.When, variables), timeZoneId, basis))
                {
                    continue;
                }
                if (!_evaluator.Evaluate<bool>(rule.Condition, NarrowVariables(rule.Condition, variables), timeZoneId, basis))
                    throw CreateRuleViolation(rule, progId);
            }
        }

        /// <summary>
        /// Builds the exception a failed rule raises: keyed for translation when both the rule and
        /// the schema have a name, literal otherwise.
        /// </summary>
        private static UserMessageException CreateRuleViolation(FormRule rule, string progId)
        {
            if (StringUtilities.IsEmpty(progId) || StringUtilities.IsEmpty(rule.RuleId))
                return new UserMessageException(rule.Message);

            string subKey = string.Format(CultureInfo.InvariantCulture, Language.FormSchemaLocalizer.RuleMessageKeyFormat, rule.RuleId);
            return new UserMessageException($"{progId}.{subKey}", rule.Message);
        }

        /// <summary>
        /// Resolves the table a rule targets: the master table when <see cref="FormRule.TargetTable"/>
        /// is empty, otherwise the named table (null when absent).
        /// </summary>
        private static FormTable? ResolveRuleTable(FormRule rule, FormSchema schema)
        {
            if (StringUtilities.IsEmpty(rule.TargetTable)) { return schema.MasterTable; }
            return schema.Tables != null && schema.Tables.Contains(rule.TargetTable)
                ? schema.Tables[rule.TargetTable] : null;
        }
    }
}
