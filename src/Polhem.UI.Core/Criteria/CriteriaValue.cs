namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// One value of an <see cref="CriteriaOperator.In"/> condition: what is compared, and what the user sees.
    /// </summary>
    /// <param name="Value">The value compared with the field, already of the field's type; for
    /// <see cref="CurrentUser"/>, a marker that the filter replaces with the signed-in user's row ids.</param>
    /// <param name="Text">The text shown for it, such as a list item's caption or a picked row's name; empty for
    /// <see cref="CurrentUser"/>, which is shown in the reader's language.</param>
    public readonly record struct CriteriaValue(object Value, string Text)
    {
        private static readonly object s_currentUser = new();

        /// <summary>
        /// Gets the value that stands for the signed-in user. It stays relative: the filter resolves it to the user's
        /// row ids each time the query runs (<see cref="GetListFilterConverter"/>), so a saved query means whoever
        /// applies it.
        /// </summary>
        public static CriteriaValue CurrentUser { get; } = new(s_currentUser, string.Empty);

        /// <summary>Gets a value indicating whether this is <see cref="CurrentUser"/>.</summary>
        public bool IsCurrentUser => ReferenceEquals(Value, s_currentUser);
    }
}
