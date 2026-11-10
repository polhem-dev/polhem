namespace Polhem.DefineEditor.ViewModels;

/// <summary>What the expression editor's try-out area shows.</summary>
public enum ExpressionTrialOutcome
{
    /// <summary>Nothing: the expression is empty or does not parse, or a sample value cannot be read.</summary>
    None,

    /// <summary>The value a field expression writes.</summary>
    Value,

    /// <summary>A rule's When or Condition holds.</summary>
    Passed,

    /// <summary>A rule's When or Condition does not hold.</summary>
    Failed,

    /// <summary>The expression fails while it is evaluated, for example by dividing by zero.</summary>
    Error,
}
