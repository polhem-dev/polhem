namespace Polhem.DefineEditor.Services;

/// <summary>
/// What the expression editor offers at the caret: the fields, helper functions and type names where a name starts,
/// the members of a value or a type after a dot, and a drop-down field's values inside the quotes it is compared with.
/// </summary>
/// <remarks>
/// It reads the text backwards from the caret and does not parse the whole expression, so it also works while the
/// expression is half typed and does not parse yet. A receiver is resolved through names, calls and dots only
/// (<c>note.Trim().</c>, <c>Today().</c>, <c>Math.</c>); anything else before a dot, such as a parenthesised
/// expression, offers nothing.
/// </remarks>
public static class ExpressionCompletion
{
    private static readonly string[] s_keywords = ["true", "false", "null"];

    /// <summary>The suggestions at <paramref name="offset"/> in <paramref name="text"/>.</summary>
    /// <param name="text">The whole expression.</param>
    /// <param name="offset">The caret.</param>
    /// <param name="sources">What the expression can name.</param>
    public static ExpressionSuggestions At(string text, int offset, ExpressionCompletionSources sources)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sources);
        offset = Math.Clamp(offset, 0, text.Length);

        if (StringLiteralStart(text, offset) is { } quote)
            return Values(text, quote, offset, sources);

        var start = offset;
        while (start > 0 && IsNameChar(text[start - 1])) start--;
        // A number such as `1.5` is not a name, and the dot in it does not reach a member.
        if (start < offset && char.IsDigit(text[start])) return ExpressionSuggestions.None(offset);

        if (start > 0 && text[start - 1] == '.')
        {
            return Receiver(text, start - 1, sources) is { } receiver
                ? new ExpressionSuggestions(ExpressionMembers.Of(receiver.Type, receiver.IsStatic), start)
                : ExpressionSuggestions.None(offset);
        }
        return new ExpressionSuggestions(Names(sources), start);
    }

    /// <summary>Whether a name starts at <paramref name="offset"/> - 1, the character the user just typed.</summary>
    /// <remarks>The editor opens the list on its own only there, not on every letter of a name.</remarks>
    public static bool StartsName(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (offset < 1 || offset > text.Length || !IsNameStart(text[offset - 1])) return false;
        if (StringLiteralStart(text, offset) is not null) return false;
        return offset == 1 || !IsNameChar(text[offset - 2]);
    }

    private static List<ExpressionSuggestion> Names(ExpressionCompletionSources sources)
    {
        var names = new List<ExpressionSuggestion>();
        names.AddRange(sources.Variables.Select(v =>
            new ExpressionSuggestion(ExpressionSuggestionKind.Field, v.Key, v.Key, ExpressionMembers.TypeName(v.Value))));
        names.AddRange(sources.Functions.Select(f =>
            new ExpressionSuggestion(ExpressionSuggestionKind.Function, f.Name,
                f.Name + (f.Parameters.Count == 0 ? "()" : "("), ExpressionMembers.Signature(f))));
        names.AddRange(ExpressionMembers.FeaturedTypes(sources.Types).Select(t =>
            new ExpressionSuggestion(ExpressionSuggestionKind.Type, t.Key, t.Key, t.Value.FullName ?? t.Value.Name)));
        names.AddRange(s_keywords.Select(k => new ExpressionSuggestion(ExpressionSuggestionKind.Keyword, k, k, string.Empty)));
        return names;
    }

    // Inside a string literal compared with a drop-down field (`status == "`), that field's values.
    private static ExpressionSuggestions Values(string text, int quote, int offset, ExpressionCompletionSources sources)
    {
        var at = quote;
        while (at > 0 && char.IsWhiteSpace(text[at - 1])) at--;
        if (at < 2 || text[at - 1] != '=' || text[at - 2] is not ('=' or '!')) return ExpressionSuggestions.None(offset);
        at -= 2;
        while (at > 0 && char.IsWhiteSpace(text[at - 1])) at--;
        var end = at;
        while (at > 0 && IsNameChar(text[at - 1])) at--;
        if (at == end || sources.ListValues(text[at..end]) is not { Count: > 0 } values) return ExpressionSuggestions.None(offset);

        var closed = offset < text.Length && text[offset] == '"';
        var items = values
            .Select(v => new ExpressionSuggestion(ExpressionSuggestionKind.Value, v, closed ? v : v + "\"", string.Empty))
            .ToList();
        return new ExpressionSuggestions(items, quote + 1);
    }

    // What a dot at `dot` is applied to: a variable, a type name or a helper call, then any chain of members and calls.
    private static (Type Type, bool IsStatic)? Receiver(string text, int dot, ExpressionCompletionSources sources)
    {
        var segments = new List<(string Name, bool IsCall)>();
        var at = dot;
        while (true)
        {
            var isCall = false;
            if (at > 0 && text[at - 1] == ')')
            {
                if (OpeningParenthesis(text, at - 1) is not { } open) return null;
                at = open;
                isCall = true;
            }
            var end = at;
            while (at > 0 && IsNameChar(text[at - 1])) at--;
            if (at == end || char.IsDigit(text[at])) return null;
            segments.Insert(0, (text[at..end], isCall));
            if (at > 0 && text[at - 1] == '.') { at--; continue; }
            break;
        }

        var (first, firstIsCall) = segments[0];
        (Type Type, bool IsStatic)? current;
        if (firstIsCall)
            current = sources.Functions.FirstOrDefault(f => f.Name == first) is { } function ? (function.ReturnType, false) : null;
        else if (sources.Variables.TryGetValue(first, out var variable))
            current = (variable, false);
        else if (sources.Types.TryGetValue(first, out var type))
            current = (type, true);
        else
            current = null;

        foreach (var (name, isCall) in segments.Skip(1))
        {
            if (current is not { } receiver) return null;
            current = ExpressionMembers.ResultType(receiver.Type, receiver.IsStatic, name, isCall) is { } next
                ? (next, false)
                : null;
        }
        return current is { Type: var resolved } && resolved != typeof(void) ? current : null;
    }

    private static int? OpeningParenthesis(string text, int close)
    {
        var depth = 0;
        for (var i = close; i >= 0; i--)
        {
            if (text[i] == ')') depth++;
            else if (text[i] == '(' && --depth == 0) return i;
        }
        return null;
    }

    // The offset of the quote that opens the string literal the caret is in; null outside a literal. A backslash
    // escapes the next character, as in C#.
    private static int? StringLiteralStart(string text, int offset)
    {
        int? open = null;
        for (var i = 0; i < offset; i++)
        {
            var c = text[i];
            if (open is not null && c == '\\') { i++; continue; }
            if (c == '"') open = open is null ? i : null;
        }
        return open;
    }

    private static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
