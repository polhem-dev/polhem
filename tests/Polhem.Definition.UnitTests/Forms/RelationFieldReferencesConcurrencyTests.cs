using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Verifies that the lazy construction of <see cref="FormTable.RelationFieldReferences"/> happens only once under concurrency.
    /// </summary>
    /// <remarks>
    /// <c>FormSchema</c> comes from the process-wide cache, so two requests touching the same schema for the first time meet here.
    /// It used to be an unprotected null check: it built the index more than once, handed different instances to different callers, and the build itself throws
    /// (when a field mapping is wrong), so the exception surfaced from what looks like a read-only property getter, at an unpredictable time.
    /// </remarks>
    public class RelationFieldReferencesConcurrencyTests
    {
        private static FormTable BuildTable()
        {
            var table = new FormTable("Order", "訂單");
            table.Fields!.Add(new FormField { FieldName = "cust_id", DbType = FieldDbType.String });
            table.Fields.Add(new FormField { FieldName = "cust_name", DbType = FieldDbType.String });

            var relation = new FormField
            {
                FieldName = "cust_ref",
                DbType = FieldDbType.String,
                Type = FieldType.DbField,
                RelationProgId = "Customer",
            };
            relation.RelationFieldMappings!.Add(new FieldMapping("sys_name", "cust_name"));
            table.Fields.Add(relation);
            return table;
        }

        [Fact]
        [DisplayName("Concurrent first reads of RelationFieldReferences build it once and every caller gets the same instance")]
        public void RelationFieldReferences_ConcurrentFirstAccess_YieldsOneInstance()
        {
            var table = BuildTable();

            var results = new RelationFieldReferenceCollection[32];
            Parallel.For(0, results.Length, i => results[i] = table.RelationFieldReferences);

            // Not "all non-null" but "all the same instance", because building it more than once was exactly the earlier defect.
            Assert.All(results, r => Assert.Same(results[0], r));
        }

        [Fact]
        [DisplayName("RelationFieldReferences builds the reverse index correctly")]
        public void RelationFieldReferences_BuildsReverseIndex()
        {
            var references = BuildTable().RelationFieldReferences;

            Assert.Single(references);
            Assert.Equal("cust_name", references[0].FieldName);
            Assert.Equal("Customer", references[0].SourceProgId);
        }
    }
}
