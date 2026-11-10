namespace Polhem.DefineEditor.Services;

/// <summary>What an expression suggestion names.</summary>
public enum ExpressionSuggestionKind
{
    /// <summary>A field of the table, a variable of the expression.</summary>
    Field,

    /// <summary>A helper function such as <c>Today()</c>.</summary>
    Function,

    /// <summary>A type an expression can name, such as <c>Math</c>.</summary>
    Type,

    /// <summary>A method, property or field reached with a dot.</summary>
    Member,

    /// <summary>A literal keyword such as <c>true</c>.</summary>
    Keyword,

    /// <summary>One of a drop-down field's values, inside a string literal.</summary>
    Value,
}
