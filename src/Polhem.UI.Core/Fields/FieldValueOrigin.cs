namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// Where a field value written into a form row came from, so a listener can tell a value the user changed from
    /// one the framework wrote as a consequence.
    /// </summary>
    /// <remarks>
    /// Only <see cref="User"/> starts a <see cref="FieldValueChangeCoordinator"/> callback: a value the framework
    /// writes in answer to a change never calls back again, which is what keeps one edit to one call.
    /// </remarks>
    public enum FieldValueOrigin
    {
        /// <summary>The user changed the value through an editor.</summary>
        User,

        /// <summary>
        /// A lookup pick or clear wrote the value: the relation field's own key or one of its mapping destinations. The
        /// data object starts the relation field's callback itself once a pick is fully written, so a response cannot
        /// land between the pick's own writes; a clear starts none.
        /// </summary>
        LookupSelection,

        /// <summary>A server callback (<c>FieldValueChange</c>) returned the value.</summary>
        ValueChangeResult,

        /// <summary>A committed row edit session republished a value it had already announced while editing.</summary>
        RowEditCommit,
    }
}
