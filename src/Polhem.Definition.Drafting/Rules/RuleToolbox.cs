using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Polhem.Core.Expressions;
using Polhem.Definition.Drafting.Session;
using Polhem.Definition.Forms;

namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// The tools a model submits a rule or an explanation with. A submission is accepted only when its expressions pass
    /// the checks for an untrusted author and the expression engine computes, for every sample, the outcome the model
    /// said it would; otherwise the reasons go back to the model.
    /// </summary>
    internal sealed class RuleToolbox
    {
        public const int MinSamples = 2;
        public const int MaxSamples = 10;
        public const int MaxDetailRows = 10;
        public const int MaxMessageLength = 500;
        public const int MaxExplanationLength = 2000;
        public const int MaxDescriptionLength = 300;
        public const int MaxValueLength = 200;
        public const int MaxValuesPerRow = 30;
        public const int MaxProblemsShown = 20;

        private static readonly Regex s_ruleId = new("^[a-z][a-z0-9_]{0,49}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly JsonSerializerOptions s_serializerOptions = CreateSerializerOptions();

        private readonly FormSchema _schema;
        private readonly FormExpressionCalculator _calculator;
        private readonly RuleDraftingOptions _options;

        public RuleToolbox(FormSchema schema, FormExpressionCalculator calculator, RuleDraftingOptions options)
        {
            _schema = schema;
            _calculator = calculator;
            _options = options;
            SubmitRule = Create(SubmitRuleCore, "submit_rule",
                "Submits the rule. It is checked and every sample is computed by the expression engine; the result says "
                + "what to fix when it is not accepted.");
            SubmitExplanation = Create(SubmitExplanationCore, "submit_explanation",
                "Submits the explanation of the rule with sample rows. Every sample is computed by the expression engine; "
                + "the result says what to fix when it is not accepted.");
        }

        public AIFunction SubmitRule { get; }

        public AIFunction SubmitExplanation { get; }

        /// <summary>Gets or sets the rule accepted in the current request; the session clears it before each request.</summary>
        public RuleDraft? AcceptedRule { get; set; }

        /// <summary>Gets or sets the explanation accepted in the current request.</summary>
        public RuleExplanation? AcceptedExplanation { get; set; }

        /// <summary>Gets or sets the rule being explained, for <see cref="SubmitExplanation"/>.</summary>
        public FormRule? ExplainedRule { get; set; }

        /// <summary>Gets or sets the id of the rule accepted earlier in the conversation, which a revision may reuse.</summary>
        public string? RevisedRuleId { get; set; }

        private string SubmitRuleCore(
            [Description("The rule id: snake_case English, such as amount_needs_approver.")] string ruleId,
            [Description("Empty for the master table, or the name of the detail table whose every row the rule checks.")] string targetTable,
            [Description("When the rule applies, as an expression; empty when it always applies.")] string when,
            [Description("What must hold for the save to go ahead, as an expression.")] string condition,
            [Description("The message shown when the condition fails, in English.")] string message,
            [Description("The message in each requested language.")] List<MessageTranslation> messageTranslations,
            [Description("What the rule does, in two or three plain sentences in the user's language, with no code.")] string explanation,
            [Description("Sample rows that show what the rule does.")] List<SampleInput> samples)
        {
            var problems = new List<string>();
            ruleId = (ruleId ?? string.Empty).Trim();
            if (!s_ruleId.IsMatch(ruleId))
                problems.Add("ruleId must be snake_case English: lowercase letters, digits and underscores, starting with a letter, at most 50 characters.");
            else if (_schema.Rules is { } rules && rules.Contains(ruleId) && ruleId != RevisedRuleId)
                problems.Add($"The form already has a rule '{ruleId}'; choose another id.");

            var table = ResolveTable(targetTable ?? string.Empty, problems);
            when = (when ?? string.Empty).Trim();
            condition = (condition ?? string.Empty).Trim();
            if (condition.Length == 0) { problems.Add("condition is required."); }
            if (table is not null)
            {
                CheckExpression(table, nameof(FormRule.When), when, problems);
                CheckExpression(table, nameof(FormRule.Condition), condition, problems);
            }

            if (string.IsNullOrWhiteSpace(message)) { problems.Add("message is required."); }
            else if (message.Length > MaxMessageLength) { problems.Add(TooLong("message", MaxMessageLength)); }
            var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var language in _options.MessageLanguages)
            {
                if (messageTranslations?.FirstOrDefault(t => t is not null && string.Equals(t.Language, language, StringComparison.OrdinalIgnoreCase))
                        is { Text: { } text } && !string.IsNullOrWhiteSpace(text))
                {
                    if (text.Length > MaxMessageLength) { problems.Add(TooLong($"The {language} message", MaxMessageLength)); }
                    else { translations[language] = text.Trim(); }
                }
                else
                    problems.Add($"messageTranslations needs the message in {language}.");
            }
            if (string.IsNullOrWhiteSpace(explanation)) { problems.Add("explanation is required."); }
            else if (explanation.Length > MaxExplanationLength) { problems.Add(TooLong("explanation", MaxExplanationLength)); }

            if (problems.Count > 0) { throw Refuse(problems); }

            var computed = ComputeSamples(table!, when, condition, samples, requireBothOutcomes: true);
            // The table as the form names it, and empty for the master table, whatever spelling the model used.
            var canonicalTable = ReferenceEquals(table, _schema.MasterTable) ? string.Empty : table!.TableName;
            AcceptedRule = new RuleDraft(ruleId, canonicalTable, when, condition, message.Trim(),
                translations, explanation.Trim(), computed);
            return "Accepted. Reply to the user in one short sentence; do not repeat the rule.";
        }

        private string SubmitExplanationCore(
            [Description("What the rule does, in two or three plain sentences in the user's language, with no code.")] string explanation,
            [Description("Sample rows that show what the rule does.")] List<SampleInput> samples)
        {
            var rule = ExplainedRule ?? throw new DraftToolException("There is no rule to explain in this request.");
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(explanation)) { problems.Add("explanation is required."); }
            else if (explanation.Length > MaxExplanationLength) { problems.Add(TooLong("explanation", MaxExplanationLength)); }
            var table = FormExpressionCalculator.ResolveRuleTable(rule, _schema);
            if (table is null) { problems.Add($"The rule's table '{rule.TargetTable}' does not exist."); }
            if (problems.Count > 0) { throw Refuse(problems); }

            // The same trimmed text CheckRule checked before the request.
            var computed = ComputeSamples(table!, rule.When?.Trim() ?? string.Empty, rule.Condition?.Trim() ?? string.Empty,
                samples, requireBothOutcomes: false);
            AcceptedExplanation = new RuleExplanation(explanation.Trim(), computed);
            return "Accepted. Reply to the user in one short sentence.";
        }

        /// <summary>
        /// Checks an existing rule's expressions as untrusted text; <see langword="null"/> when they pass.
        /// </summary>
        public string? CheckRule(FormRule rule)
        {
            var table = FormExpressionCalculator.ResolveRuleTable(rule, _schema);
            if (table is null) { return $"its table '{Echo(rule.TargetTable)}' is not a table of the form."; }
            if (string.IsNullOrWhiteSpace(rule.Condition)) { return "it has no condition."; }
            var problems = new List<string>();
            CheckExpression(table, nameof(FormRule.When), rule.When?.Trim() ?? string.Empty, problems);
            CheckExpression(table, nameof(FormRule.Condition), rule.Condition?.Trim() ?? string.Empty, problems);
            return problems.Count == 0 ? null : string.Join(" ", problems);
        }

        private FormTable? ResolveTable(string targetTable, List<string> problems)
        {
            targetTable = targetTable.Trim();
            if (targetTable.Length == 0)
            {
                if (_schema.MasterTable is { } master) { return master; }
                problems.Add("The form has no master table.");
                return null;
            }
            if (_schema.Tables is { } tables && tables.Contains(targetTable)) { return tables[targetTable]; }
            problems.Add($"targetTable '{Echo(targetTable)}' is not a table of the form. Tables: {string.Join(", ", (_schema.Tables ?? []).Select(t => t.TableName))}.");
            return null;
        }

        private void CheckExpression(FormTable table, string propertyName, string expression, List<string> problems)
        {
            if (expression.Length == 0) { return; }
            try
            {
                _calculator.ValidateUntrustedExpression(_schema, table, propertyName, expression);
            }
            catch (ExpressionEvaluationException ex)
            {
                problems.Add($"{propertyName} is not accepted: {ex.Message}");
            }
        }

        private List<RuleDraftSample> ComputeSamples(FormTable table, string when, string condition,
            List<SampleInput>? samples, bool requireBothOutcomes)
        {
            samples = samples?.Where(sample => sample is not null).ToList();
            if (samples is null || samples.Count < MinSamples || samples.Count > MaxSamples)
                throw Refuse([$"Give between {MinSamples} and {MaxSamples} samples."]);

            var computed = new List<RuleDraftSample>();
            var problems = new List<string>();
            for (int i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];
                var label = string.Create(CultureInfo.InvariantCulture, $"Sample {i + 1}");
                if (string.IsNullOrWhiteSpace(sample.Description)) { problems.Add($"{label} needs a description."); }
                else if (sample.Description.Length > MaxDescriptionLength)
                {
                    problems.Add(TooLong($"{label}'s description", MaxDescriptionLength));
                    continue;
                }
                // A field a sample leaves out is empty, so a sample may give no values at all: every field is empty. A
                // value the model left out by mistake changes the computed outcome, which the comparison below refuses.
                var values = ReadValues(table, sample.Values, label, problems);
                var detailRows = ReadDetailRows(table, sample.DetailRows, label, problems);
                if (values is null || detailRows is null) { continue; }
                RuleSampleOutcome outcome;
                try
                {
                    outcome = when.Length > 0 && !Evaluate(when, table, values, detailRows)
                        ? RuleSampleOutcome.Skipped
                        : Evaluate(condition, table, values, detailRows)
                            ? RuleSampleOutcome.Passes
                            : RuleSampleOutcome.Blocked;
                }
                catch (ExpressionEvaluationException ex)
                {
                    problems.Add($"{label} cannot be computed: {ex.Message}");
                    continue;
                }
                catch (Exception ex) when (ex is ArithmeticException or ArgumentException or InvalidCastException
                    or FormatException or IndexOutOfRangeException or NullReferenceException or InvalidOperationException
                    or KeyNotFoundException)
                {
                    // A save with these values would fail with an error instead of the rule's message, so the rule
                    // must handle them (for example by testing a divisor before dividing). The calculator's own
                    // preconditions (a non-empty expression, a known detail table) are checked before this point, so what
                    // arrives here is raised by the rule itself.
                    problems.Add($"{label} makes the rule fail when it runs ({ex.GetType().Name}: {ex.Message}). A record "
                        + "with these values could not be saved at all; change the expression so it handles them.");
                    continue;
                }
                if (outcome != sample.Expected)
                    problems.Add($"{label} ({Echo(sample.Description)}): the engine computes {outcome}, not {sample.Expected}. Fix the rule or the sample.");
                computed.Add(new RuleDraftSample(sample.Description?.Trim() ?? string.Empty, values, detailRows, outcome));
            }
            if (problems.Count == 0 && requireBothOutcomes
                && (!computed.Exists(s => s.Outcome == RuleSampleOutcome.Passes) || !computed.Exists(s => s.Outcome == RuleSampleOutcome.Blocked)))
                problems.Add("Include at least one sample that passes and one that is blocked.");
            if (problems.Count > 0) { throw Refuse(problems); }
            return computed;
        }

        // A master rule reads the sample's detail rows through its aggregates; a detail table's rule reads one row.
        private bool Evaluate(string expression, FormTable table,
            Dictionary<string, object?> values, Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> detailRows)
            => ReferenceEquals(table, _schema.MasterTable)
                ? _calculator.EvaluateMasterRuleSample(expression, _schema,
                    new ExpressionSample { Values = values, DetailRows = detailRows, TimeZoneId = _options.TimeZoneId })
                : _calculator.EvaluateRuleSample(expression, table,
                    new ExpressionSample { Values = values, TimeZoneId = _options.TimeZoneId });

        private Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>? ReadDetailRows(FormTable table,
            List<SampleDetailRow>? input, string label, List<string> problems)
        {
            var rows = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>(StringComparer.Ordinal);
            if (input is not { Count: > 0 }) { return rows; }
            if (!ReferenceEquals(table, _schema.MasterTable))
            {
                problems.Add($"{label} has detail rows, but only a rule on the master table reads them.");
                return null;
            }
            if (input.Count > MaxDetailRows)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{label} has more than {MaxDetailRows} detail rows."));
                return null;
            }
            var lists = new Dictionary<string, List<IReadOnlyDictionary<string, object?>>>(StringComparer.Ordinal);
            var ok = true;
            foreach (var row in input)
            {
                if (row is null) { continue; }
                if (_schema.Tables is not { } tables || !tables.Contains(row.Table ?? string.Empty)
                    || ReferenceEquals(tables[row.Table!], _schema.MasterTable))
                {
                    problems.Add($"{label} has a detail row of '{Echo(row.Table)}', which is not a detail table of the form.");
                    ok = false;
                    continue;
                }
                var detailTable = tables[row.Table!];
                var values = ReadValues(detailTable,
                    (row.Values ?? []).Where(v => v is not null).Select(v => new SampleValue { Field = v.Field, Value = v.Value }).ToList(),
                    label, problems);
                if (values is null) { ok = false; continue; }
                if (!lists.TryGetValue(detailTable.TableName, out var list))
                {
                    list = [];
                    lists[detailTable.TableName] = list;
                }
                list.Add(values);
            }
            foreach (var (name, list) in lists) { rows[name] = list; }
            return ok ? rows : null;
        }

        // The values of a sample, by the field's declared name and coerced as a cell read from the database is.
        private static Dictionary<string, object?>? ReadValues(FormTable table, List<SampleValue>? input,
            string label, List<string> problems)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (input is null) { return values; }
            if (input.Count > MaxValuesPerRow)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{label} gives more than {MaxValuesPerRow} values in one row."));
                return null;
            }
            var ok = true;
            foreach (var item in input)
            {
                if (item is null) { continue; }
                var name = item.Field ?? string.Empty;
                if (item.Value is { Length: > MaxValueLength })
                {
                    problems.Add(TooLong($"{label}'s value for {Echo(name)}", MaxValueLength));
                    ok = false;
                    continue;
                }
                if (table.Fields is null || !table.Fields.Contains(name))
                {
                    problems.Add($"{label} names '{Echo(name)}', which is not a field of {table.TableName}.");
                    ok = false;
                    continue;
                }
                var field = table.Fields[name];
                try
                {
                    // The value arrives as text, read as a cell is: invariant culture, ISO dates. Empty text is an empty field,
                    // whatever its type, so a sample can show a date or a number left blank.
                    values[field.FieldName] = ExpressionPolicy.CoerceValue(
                        string.IsNullOrEmpty(item.Value) ? null : item.Value, field.DbType);
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                {
                    problems.Add($"{label}: '{Echo(item.Value)}' is not a value for {Echo(name)} ({field.DbType}).");
                    ok = false;
                }
            }
            return ok ? values : null;
        }

        private static string TooLong(string what, int max) =>
            string.Create(CultureInfo.InvariantCulture, $"{what} is longer than {max} characters.");

        // What the model sent, echoed in a refusal at a bounded length, so a huge argument does not grow the history.
        private static string Echo(string? text) =>
            text is null ? string.Empty : text.Length <= 60 ? text : text[..60] + "…";

        // At most MaxProblemsShown lines, so a submission with thousands of faults cannot grow the history without bound.
        private static DraftToolException Refuse(IEnumerable<string> problems)
        {
            var list = problems.ToList();
            var shown = list.Take(MaxProblemsShown).ToList();
            if (list.Count > shown.Count)
                shown.Add(string.Create(CultureInfo.InvariantCulture, $"…and {list.Count - shown.Count} more."));
            return new("Not accepted:\n- " + string.Join("\n- ", shown));
        }

        private static AIFunction Create(Delegate method, string name, string description)
            => AIFunctionFactory.Create(method, new AIFunctionFactoryOptions
            {
                Name = name,
                Description = description,
                SerializerOptions = s_serializerOptions,
            });

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions);
            options.Converters.Add(new JsonStringEnumConverter());
            options.MakeReadOnly();
            return options;
        }

        /// <summary>A rule message in one language.</summary>
        internal sealed class MessageTranslation
        {
            /// <summary>The culture name.</summary>
            [Description("The culture name, such as zh-TW.")]
            public required string Language { get; set; }

            /// <summary>The message in that language.</summary>
            [Description("The message in that language.")]
            public required string Text { get; set; }
        }

        /// <summary>One field's value in a sample row.</summary>
        internal sealed class SampleValue
        {
            /// <summary>The field name.</summary>
            [Description("The field name, exactly as listed.")]
            public required string Field { get; set; }

            /// <summary>The value as text.</summary>
            [Description("The value as text: a number such as 200000 or 1.5, a date as yyyy-MM-dd, true or false, or any text.")]
            public required string Value { get; set; }
        }

        /// <summary>
        /// One field's value in a sample's detail row. The same shape as <see cref="SampleValue"/>, as a type of its own:
        /// a type used twice in a tool's schema becomes a reference to its first place, which the Claude API refuses.
        /// </summary>
        internal sealed class DetailValue
        {
            /// <summary>The field name.</summary>
            [Description("The detail field name, exactly as listed.")]
            public required string Field { get; set; }

            /// <summary>The value as text.</summary>
            [Description("The value as text: a number such as 2 or 1.5, a date as yyyy-MM-dd, true or false, or any text.")]
            public required string Value { get; set; }
        }

        /// <summary>One detail row of a sample.</summary>
        internal sealed class SampleDetailRow
        {
            /// <summary>The detail table.</summary>
            [Description("The detail table's name.")]
            public required string Table { get; set; }

            /// <summary>The values of the row.</summary>
            [Description("The values of the detail fields the rule reads.")]
            public required List<DetailValue> Values { get; set; }
        }

        /// <summary>One sample row as the model writes it.</summary>
        internal sealed class SampleInput
        {
            /// <summary>What the sample shows, in the user's language.</summary>
            [Description("What the sample shows, in the user's language, such as \"An amount over the limit with no approver\".")]
            public required string Description { get; set; }

            /// <summary>The values of the fields the rule reads.</summary>
            [Description("The values of the fields the rule reads; a field left out is empty.")]
            public required List<SampleValue> Values { get; set; }

            /// <summary>The sample's detail rows.</summary>
            [Description("For a master rule that reads a detail table (Count, Sum, Any, All...), the detail rows of the sample; otherwise an empty list.")]
            public required List<SampleDetailRow> DetailRows { get; set; }

            /// <summary>What the model expects the rule to do with the row.</summary>
            [Description("What the rule should do with this row: Passes, Blocked, or Skipped (when the rule does not apply).")]
            public required RuleSampleOutcome Expected { get; set; }
        }
    }
}
