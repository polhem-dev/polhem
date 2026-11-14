using System.Text;

namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// Writes the names a model proposes in the form the framework expects: ProgIds and table names in PascalCase,
    /// field names in snake_case, both limited to ASCII letters, digits and, in field names, underscores.
    /// </summary>
    internal static class DraftNaming
    {
        public static string ToPascalCase(string? text)
        {
            var builder = new StringBuilder();
            foreach (var word in Words(text))
            {
                builder.Append(char.ToUpperInvariant(word[0]));
                builder.Append(word, 1, word.Length - 1);
            }
            if (builder.Length > 0 && char.IsAsciiDigit(builder[0]))
                builder.Insert(0, 'F');
            return builder.ToString();
        }

        public static string ToSnakeCase(string? text)
        {
            var words = Words(text).SelectMany(SplitCamelCase).Select(word => word.ToLowerInvariant()).ToList();
            var name = string.Join('_', words);
            if (name.Length > 0 && char.IsAsciiDigit(name[0]))
                name = "f_" + name;
            return name;
        }

        /// <summary>
        /// Splits text into runs of ASCII letters and digits. Everything else, other scripts included, separates words.
        /// </summary>
        private static IEnumerable<string> Words(string? text)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            var start = -1;
            for (var index = 0; index <= text.Length; index++)
            {
                var isWordChar = index < text.Length && char.IsAsciiLetterOrDigit(text[index]);
                if (isWordChar && start < 0)
                {
                    start = index;
                }
                else if (!isWordChar && start >= 0)
                {
                    yield return text[start..index];
                    start = -1;
                }
            }
        }

        /// <summary>
        /// Splits <c>leaveType</c> into <c>leave</c> and <c>Type</c>, and <c>HTTPServer</c> into <c>HTTP</c> and
        /// <c>Server</c>.
        /// </summary>
        private static IEnumerable<string> SplitCamelCase(string word)
        {
            var start = 0;
            for (var index = 1; index < word.Length; index++)
            {
                var current = word[index];
                var previous = word[index - 1];
                var startsWord = char.IsAsciiLetterUpper(current)
                    && (char.IsAsciiLetterLower(previous) || char.IsAsciiDigit(previous)
                        || (char.IsAsciiLetterUpper(previous) && index + 1 < word.Length && char.IsAsciiLetterLower(word[index + 1])));
                if (startsWord)
                {
                    yield return word[start..index];
                    start = index;
                }
            }
            yield return word[start..];
        }
    }
}
