using Polhem.Api.Contracts.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Output result for the FormSchema-driven <c>Delete</c> operation.
    /// </summary>
    public sealed class DeleteResult : BusinessResult, IDeleteResponse
    {
        /// <summary>
        /// Gets or sets the number of master rows actually deleted.
        /// </summary>
        public int RowsAffected { get; set; }
    }
}
