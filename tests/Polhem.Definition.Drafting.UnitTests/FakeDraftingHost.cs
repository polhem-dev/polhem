using Polhem.Core.Data;

namespace Polhem.Definition.Drafting.UnitTests
{
    /// <summary>
    /// A host with a small catalog of existing forms and validation results the test decides.
    /// </summary>
    internal sealed class FakeDraftingHost : IFormDraftingHost
    {
        private readonly List<DraftCatalogEntry> _catalog =
        [
            new("Employee", "Employee", "員工",
                [new("sys_id", "Employee No.", FieldDbType.String), new("sys_name", "Name", FieldDbType.String)]),
            new("Department", "Department", "部門",
                [new("sys_id", "Department No.", FieldDbType.String), new("sys_name", "Name", FieldDbType.String)]),
            new("Customer", "Customer", "客戶",
                [new("sys_id", "Customer No.", FieldDbType.String), new("sys_name", "Name", FieldDbType.String)]),
        ];

        /// <summary>Gets the issues <see cref="Validate"/> returns.</summary>
        public List<DraftIssue> ValidationIssues { get; } = [];

        /// <summary>Gets how many times <see cref="Validate"/> was called.</summary>
        public int ValidateCalls { get; private set; }

        public IReadOnlyList<DraftCatalogEntry> GetCatalog() => _catalog;

        public IReadOnlyList<DraftCatalogField> GetFields(string progId)
            => _catalog.FirstOrDefault(entry => entry.ProgId == progId)?.Fields ?? [];

        public IReadOnlyList<DraftIssue> Validate(FormDraftResult result)
        {
            ValidateCalls++;
            return ValidationIssues;
        }
    }
}
