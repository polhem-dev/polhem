using Polhem.Definition.Filters;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;

namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract interface for the GetList request.
    /// </summary>
    /// <remarks>
    /// The target table is always the master table of the schema identified by
    /// <c>ProgId</c> (the framework enforces <c>FormSchema.MasterTable.TableName == ProgId</c>),
    /// so no table name is carried on the request.
    /// </remarks>
    public interface IGetListRequest
    {
        /// <summary>
        /// Gets the comma-separated field names; an empty value falls back to
        /// <see cref="Polhem.Definition.Forms.FormSchema.ListFields"/>, then to all fields.
        /// </summary>
        string SelectFields { get; }

        /// <summary>
        /// Gets the filter condition tree; <c>null</c> indicates an unfiltered query.
        /// </summary>
        FilterNode? Filter { get; }

        /// <summary>
        /// Gets the sort field collection; <c>null</c> uses the default ordering.
        /// </summary>
        SortFieldCollection? SortFields { get; }

        /// <summary>
        /// Gets the paging options; <c>null</c> is served as the first page of
        /// <see cref="Polhem.Definition.Paging.PagingOptions.MaxPageSize"/> rows.
        /// </summary>
        PagingOptions? Paging { get; }
    }
}
