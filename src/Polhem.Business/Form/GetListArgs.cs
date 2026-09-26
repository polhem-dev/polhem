using System.Text.Json.Serialization;
using Polhem.Api.Contracts.Form;
using Polhem.Definition.Filters;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Input arguments for retrieving FormSchema-driven list-view rows from the
    /// master table of <c>ProgId</c>.
    /// </summary>
    public class GetListArgs : BusinessArgs, IGetListRequest
    {
        /// <summary>
        /// Gets or sets the comma-separated field names; an empty value falls back to
        /// <see cref="Polhem.Definition.Forms.FormSchema.ListFields"/>, then to all fields.
        /// </summary>
        public string SelectFields { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the filter condition tree; <c>null</c> indicates an unfiltered query.
        /// </summary>
        [JsonConverter(typeof(FilterNodeJsonConverter))]
        public FilterNode? Filter { get; set; }

        /// <summary>
        /// Gets or sets the sort field collection; <c>null</c> uses the default ordering.
        /// </summary>
        public SortFieldCollection? SortFields { get; set; }

        /// <summary>
        /// Gets or sets the paging options; <c>null</c> is served as the first page of
        /// <see cref="Polhem.Definition.Paging.PagingOptions.MaxPageSize"/> rows.
        /// </summary>
        public PagingOptions? Paging { get; set; }
    }
}
