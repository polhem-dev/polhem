using System.Globalization;
using System.Text;
using Polhem.Definition.Forms;

namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// The texts the rule drafting session sends the model besides the user's own words.
    /// </summary>
    internal static class RuleDraftingPrompt
    {
        private const string Syntax = """
            Expressions are a subset of C#:
            - A field is written by its field name exactly, as listed below (names are case-sensitive).
            - Operators: + - * / %, == != < <= > >=, && || !, and the conditional a ? b : c. Text literals are in double
              quotes. A whole number is fine next to an amount; a number with a decimal point needs the m suffix (1.5m).
            - An empty text field is "" and an empty amount is 0; neither is null. IsBlank(value) tests "not filled in".
            - A key field (a pick from another form) is a Guid; test it with IsBlank(field) or field != Guid.Empty.
            - A date field is a date and time; Today() is a date. Compare them with DaysBetween, Between, or the
              members below.
            - Members you may use: on text Length, Contains, StartsWith, EndsWith, Trim, ToUpper, ToLower, Substring; on
              dates Year, Month, Day, DayOfWeek, AddDays, AddMonths, AddYears, Date; Math.Round, Math.Abs, Math.Max,
              Math.Min, Math.Floor, Math.Ceiling. Nothing else: no GetType, typeof, DateTime.Now, LINQ, RegexMatch or
              string.Format. At most 1,000 characters, 16 levels of parentheses and 8 text joins.
            - A rule on the master table may read the detail tables with Sum(Table.field), Count(Table),
              Min(Table.field), Max(Table.field), and Count(Table, condition), Any(Table, condition),
              All(Table, condition), where the condition names the detail table's fields.
            """;

        public static string DraftSystemPrompt(FormSchema schema, RuleDraftingOptions options) => $$"""
            You write one validation rule for a data entry form of the Polhem framework, from what the user says it should
            check. The user is not a developer: they describe the rule in their own words, and they will judge your work by
            your plain explanation and by sample rows the program computes, not by reading the expression.

            A rule has:
            - targetTable: empty to check the master record once per save, or a detail table's name to check each of its
              rows. A detail table's rule reads only that table's fields.
            - when (optional): the rule applies only when this is true.
            - condition: what must hold; when it is false the save stops and the user sees the message.
            - message: what the user sees, in English, and its translation into {{Languages(options)}}.

            {{Syntax}}

            Functions:
            {{Functions(options)}}

            The form:
            {{Fields(schema)}}

            Work like this:
            1. If what the user wants is unclear in a way that changes the rule, ask one short question in a reply,
               without calling a tool. Otherwise write the rule straight away.
            2. Call submit_rule with the rule, the explanation in {{ReplyLanguage(options)}}, and three to six samples:
               rows of values with what the rule should do with each (Passes, Blocked, or Skipped when it does not
               apply), at least one that passes and one that is blocked. A sample holds the fields the rule reads from
               its table; a master rule that reads a detail table also gives the sample's detail rows (a rule on a detail
               table, and a rule that reads none, gives an empty list).
            3. If the result is not accepted, fix what it names and call submit_rule again.
            4. When it is accepted, reply in one short sentence in {{ReplyLanguage(options)}}.
            Write descriptions and replies in {{ReplyLanguage(options)}}, with no code and no Markdown.
            """;

        public static string ExplainSystemPrompt(FormSchema schema, RuleDraftingOptions options) => $$"""
            You explain an existing validation rule of a data entry form of the Polhem framework to a user who is not a
            developer, and show what it does with sample rows the program computes.

            A rule has a targetTable (empty for the master record, or a detail table whose every row it checks), an
            optional when (the rule applies only when it is true) and a condition that must hold, or the save stops.

            {{Syntax}}

            Functions:
            {{Functions(options)}}

            The form:
            {{Fields(schema)}}

            Call submit_explanation with two or three plain sentences in {{ReplyLanguage(options)}}, with no code, and
            three to six samples: rows of values with what the rule does with each (Passes, Blocked, or Skipped when it
            does not apply). A sample holds the fields the rule reads from its table, and, for a master rule that reads a
            detail table, the sample's detail rows. If the result is not
            accepted, fix what it names and call again. When it is accepted, reply in one short sentence.
            """;

        /// <summary>The user's first message, with the language to reply in when one is set.</summary>
        public static string FirstMessage(string text, RuleDraftingOptions options) =>
            options.ReplyCulture is { } culture
                ? $"{text}\n\n(Reply in {culture.EnglishName}, and write the explanation and the sample descriptions in it.)"
                : text;

        public static string ExplainRequest(FormRule rule, RuleDraftingOptions options) => string.Create(CultureInfo.InvariantCulture, $"""
            Explain this rule{(options.ReplyCulture is { } culture ? $", in {culture.EnglishName}" : string.Empty)}.
            targetTable: {rule.TargetTable}
            when: {rule.When}
            condition: {rule.Condition}
            message: {rule.Message}
            """);

        private static string Languages(RuleDraftingOptions options) =>
            options.MessageLanguages.Count == 0 ? "no other language" : string.Join(", ", options.MessageLanguages);

        private static string ReplyLanguage(RuleDraftingOptions options) =>
            options.ReplyCulture is { } culture ? $"{culture.EnglishName} ({culture.Name})" : "English";

        private static string Functions(RuleDraftingOptions options)
        {
            var builder = new StringBuilder();
            foreach (var function in options.Functions)
                builder.Append(CultureInfo.InvariantCulture, $"- {function.Name}{function.Signature}: {function.Summary} Example: {function.Example}\n");
            return builder.Length == 0 ? "(none)" : builder.ToString().TrimEnd();
        }

        private static string Fields(FormSchema schema)
        {
            var builder = new StringBuilder();
            foreach (var table in schema.Tables ?? [])
            {
                var role = ReferenceEquals(table, schema.MasterTable) ? "master table" : "detail table";
                builder.Append(CultureInfo.InvariantCulture, $"{table.TableName} ({role}):\n");
                foreach (var field in table.Fields ?? [])
                {
                    if (field.FieldName.StartsWith("sys_", StringComparison.Ordinal)) { continue; }
                    builder.Append(CultureInfo.InvariantCulture, $"- {field.FieldName}: {field.DbType}, \"{field.Caption}\"");
                    if (field.ListItems is { Count: > 0 } items)
                        builder.Append(CultureInfo.InvariantCulture, $", one of {string.Join(", ", items.Select(i => $"\"{i.Value}\""))}");
                    builder.Append('\n');
                }
            }
            return builder.ToString().TrimEnd();
        }
    }
}
