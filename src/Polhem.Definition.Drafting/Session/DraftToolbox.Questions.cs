using System.ComponentModel;

namespace Polhem.Definition.Drafting.Session
{
    /// <summary>
    /// The tool that asks the user to decide something, with answers to choose from.
    /// </summary>
    internal sealed partial class DraftToolbox
    {
        private const int MaxQuestions = 6;
        private const int MinOptions = 2;
        private const int MaxOptions = 5;

        /// <summary>
        /// Gets or sets the questions the last tool call asked the user, or <see langword="null"/>. The session reads and
        /// clears it after each call.
        /// </summary>
        public IReadOnlyList<DraftQuestion>? AskedQuestions { get; set; }

        private string AskUser(
            [Description("The decisions the user has to make, a few at a time.")] AskUserQuestion[] questions)
        {
            if (questions.Length == 0)
                throw new DraftToolException("Ask at least one question.");
            if (questions.Length > MaxQuestions)
                throw new DraftToolException($"Ask at most {MaxQuestions} questions at a time; ask the rest after the answers.");

            var asked = new List<DraftQuestion>();
            for (var i = 0; i < questions.Length; i++)
                asked.Add(CheckQuestion(questions[i], i + 1));

            AskedQuestions = asked;
            return $"The {(asked.Count == 1 ? "question is" : "questions are")} shown to the user below your reply, with the "
                + "options to choose from. Their answers arrive in their next message. Do not call more tools in this turn: "
                + "end it with one short sentence, and do not repeat the questions in it.";
        }

        private static DraftQuestion CheckQuestion(AskUserQuestion question, int number)
        {
            var text = question.Question.Trim();
            if (text.Length == 0)
                throw new DraftToolException($"Question {number} has no text.");
            var options = question.Options
                .Select(option => new DraftQuestionOption(option.Label.Trim(), option.Description.Trim()))
                .ToList();
            if (options.Count < MinOptions || options.Count > MaxOptions)
                throw new DraftToolException($"Question {number} needs {MinOptions} to {MaxOptions} options, not {options.Count}. "
                    + "The user can always answer in their own words, so offer only the likely answers.");
            if (options.Any(option => option.Label.Length == 0))
                throw new DraftToolException($"Every option of question {number} needs a label.");
            if (options.Select(option => option.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
                throw new DraftToolException($"Two options of question {number} have the same label.");

            var recommended = question.Recommended.Trim();
            var recommendedIndex = recommended.Length == 0
                ? -1
                : options.FindIndex(option => string.Equals(option.Label, recommended, StringComparison.OrdinalIgnoreCase));
            if (recommended.Length > 0 && recommendedIndex < 0)
                throw new DraftToolException($"The recommended answer of question {number}, '{recommended}', is not the label of one "
                    + "of its options.");
            return new DraftQuestion(text, options, recommendedIndex);
        }

        // IMPORTANT: every property is `required`, so the schema lists it as required. Strict tool schemas limit how many
        // optional parameters all the tools may have together; `DraftToolSchemaTests` counts them.

        /// <summary>A question as the model writes it in a call of <c>ask_user</c>.</summary>
        internal sealed class AskUserQuestion
        {
            [Description("The question, in the language of the conversation, such as 'Should the department field pick a record from Department?'.")]
            public required string Question { get; set; }

            [Description("The likely answers, each short enough for a button.")]
            public required AskUserOption[] Options { get; set; }

            [Description("The label of the option you suggest, exactly as written in options; empty when you have no suggestion.")]
            public required string Recommended { get; set; }
        }

        /// <summary>An answer offered in a call of <c>ask_user</c>.</summary>
        internal sealed class AskUserOption
        {
            [Description("The answer, a few words, such as 'Yes' or 'LeaveRequest'.")]
            public required string Label { get; set; }

            [Description("What choosing it means, in one short sentence; empty when the label says it all.")]
            public required string Description { get; set; }
        }
    }
}
