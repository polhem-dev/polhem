namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// One value of an <see cref="CriteriaOperator.In"/> condition: what is compared, and what the user sees.
    /// </summary>
    /// <param name="Value">The value compared with the field, already of the field's type.</param>
    /// <param name="Text">The text shown for it, such as a list item's caption or a picked row's name.</param>
    public readonly record struct CriteriaValue(object Value, string Text);
}
