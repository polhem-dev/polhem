using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Logging
{
    /// <summary>
    /// <see cref="AuditEntry"/> / <see cref="NullAuditLogWriter"/> / <see cref="AuditLogOptions"/>
    /// Unit tests: they verify the assembly of the common columns, the no-op writer behavior and the default options.
    /// </summary>
    public class AuditLoggingTests
    {
        private sealed class TestAuditEntry : AuditEntry
        {
            public override string TableName => "st_log_test";

            public string? Extra { get; init; }

            protected override void AddColumns(IList<AuditColumn> columns)
                => columns.Add(new AuditColumn("extra", Extra));
        }

        [Fact]
        [DisplayName("GetColumns contains the common columns (including the denormalized user_name/company_name) followed by the subclass columns")]
        public void GetColumns_IncludesCommonAndSubclassColumns()
        {
            var rowId = Guid.NewGuid();
            var entry = new TestAuditEntry
            {
                SysRowId = rowId,
                UserId = "demo",
                UserName = "Demo User",
                CompanyId = "c1",
                CompanyName = "Company One",
                Source = "Test.Action",
                Extra = "x",
            };

            var map = entry.GetColumns().ToDictionary(c => c.Name, c => c.Value);

            Assert.Equal("st_log_test", entry.TableName);
            Assert.Equal(rowId, map["sys_rowid"]);
            Assert.True(map.ContainsKey("log_time"));
            Assert.Equal("demo", map["user_id"]);
            Assert.Equal("Demo User", map["user_name"]);
            Assert.Equal("c1", map["company_id"]);
            Assert.Equal("Company One", map["company_name"]);
            Assert.Equal("Test.Action", map["source"]);
            Assert.Equal("x", map["extra"]);
            // Log rows are self-sufficient — no bare user_rowid that would need a join.
            Assert.False(map.ContainsKey("user_rowid"));
        }

        [Fact]
        [DisplayName("GetColumns order: sys_rowid is the first column and the subclass columns come last")]
        public void GetColumns_OrdersCommonFirstThenSubclass()
        {
            var columns = new TestAuditEntry { Extra = "x" }.GetColumns();

            Assert.Equal("sys_rowid", columns[0].Name);
            Assert.Equal("extra", columns[^1].Name);
        }

        [Fact]
        [DisplayName("LogTimeUtc defaults to a UTC time")]
        public void LogTimeUtc_DefaultsToUtc()
        {
            var entry = new TestAuditEntry();

            Assert.Equal(DateTimeKind.Utc, entry.LogTimeUtc.Kind);
        }

        [Fact]
        [DisplayName("LoginAuditEntry has the right target table and event/fail_reason columns")]
        public void LoginAuditEntry_ColumnsAndTable()
        {
            var entry = new LoginAuditEntry
            {
                UserId = "demo",
                UserName = "Demo User",
                Event = LoginEvent.LoginFailed,
                FailReason = "Invalid username or password.",
            };

            var map = entry.GetColumns().ToDictionary(c => c.Name, c => c.Value);

            Assert.Equal("st_log_login", entry.TableName);
            Assert.Equal((int)LoginEvent.LoginFailed, map["event"]);
            Assert.Equal("Invalid username or password.", map["fail_reason"]);
            Assert.Equal("demo", map["user_id"]);
            Assert.Equal("Demo User", map["user_name"]);
        }

        [Fact]
        [DisplayName("GetColumns common columns include the calling application's identity (api_key_id / api_key_name)")]
        public void GetColumns_IncludesApiKeyIdentity()
        {
            var entry = new TestAuditEntry
            {
                ApiKeyId = "northwind-desktop",
                ApiKeyName = "Northwind Desktop",
                Extra = "x",
            };

            var map = entry.GetColumns().ToDictionary(c => c.Name, c => c.Value);

            Assert.Equal("northwind-desktop", map["api_key_id"]);
            Assert.Equal("Northwind Desktop", map["api_key_name"]);
        }

        [Fact]
        [DisplayName("For a call that did not pass the key gate, api_key_id / api_key_name are null rather than empty strings")]
        public void GetColumns_WithoutApiKey_LeavesIdentityNull()
        {
            var map = new TestAuditEntry { Extra = "x" }.GetColumns().ToDictionary(c => c.Name, c => c.Value);

            Assert.Null(map["api_key_id"]);
            Assert.Null(map["api_key_name"]);
        }

        [Fact]
        [DisplayName("DbAnomalyEntry has no common columns and therefore no calling application identity")]
        public void DbAnomalyEntry_OmitsApiKeyIdentity()
        {
            // A DB anomaly is seen from `database_id` + `command`, with no notion of who is calling. It overrides the whole set of common columns,
            // so the new identity columns naturally do not appear in `st_log_anomaly_db`.
            var entry = new DbAnomalyEntry
            {
                DatabaseId = "common",
                Command = "SELECT 1",
                Kind = AnomalyKind.Error,
            };

            var names = entry.GetColumns().Select(c => c.Name).ToList();

            Assert.DoesNotContain("api_key_id", names);
            Assert.DoesNotContain("api_key_name", names);
            Assert.DoesNotContain("user_id", names);
        }

        [Fact]
        [DisplayName("ChangeAuditEntry has the right target table and prog_id/table_name/row_key/change_kind/is_sensitive/changes_xml columns")]
        public void ChangeAuditEntry_ColumnsAndTable()
        {
            var entry = new ChangeAuditEntry
            {
                ProgId = "Employee",
                ChangeTableName = "st_employee",
                RowKey = "abc",
                ChangeKind = ChangeKind.Update,
                IsSensitive = false,
                ChangesXml = "<diffgr:diffgram />",
            };

            var map = entry.GetColumns().ToDictionary(c => c.Name, c => c.Value);

            Assert.Equal("st_log_change", entry.TableName);
            Assert.Equal("Employee", map["prog_id"]);
            Assert.Equal("st_employee", map["table_name"]);
            Assert.Equal("abc", map["row_key"]);
            Assert.Equal((int)ChangeKind.Update, map["change_kind"]);
            Assert.False((bool)map["is_sensitive"]!);
            Assert.Equal("<diffgr:diffgram />", map["changes_xml"]);
        }

        [Fact]
        [DisplayName("AccessAuditEntry has the right target table and prog_id/row_key columns (no field values recorded)")]
        public void AccessAuditEntry_ColumnsAndTable()
        {
            var entry = new AccessAuditEntry
            {
                UserId = "demo",
                UserName = "Demo User",
                ProgId = "Order",
                RowKey = "abc",
            };

            var map = entry.GetColumns().ToDictionary(c => c.Name, c => c.Value);

            Assert.Equal("st_log_access", entry.TableName);
            Assert.Equal("Order", map["prog_id"]);
            Assert.Equal("abc", map["row_key"]);
            Assert.Equal("demo", map["user_id"]);
            Assert.Equal("Demo User", map["user_name"]);
            // Record-level only — no field-level column.
            Assert.False(map.ContainsKey("sensitive_fields"));
        }

        [Fact]
        [DisplayName("ApiAnomalyEntry has its target table and method/kind/timing columns, plus the common who columns")]
        public void ApiAnomalyEntry_Columns()
        {
            var entry = new ApiAnomalyEntry
            {
                UserId = "demo",
                Method = "Order.Save",
                Kind = AnomalyKind.Slow,
                ElapsedMs = 5000,
                ThresholdMs = 3000,
            };

            var map = entry.GetColumns().ToDictionary(c => c.Name, c => c.Value);

            Assert.Equal("st_log_anomaly_api", entry.TableName);
            Assert.Equal("Order.Save", map["method"]);
            Assert.Equal((int)AnomalyKind.Slow, map["anomaly_kind"]);
            Assert.Equal(5000, map["elapsed_ms"]);
            Assert.Equal(3000, map["threshold_ms"]);
            Assert.True(map.ContainsKey("user_id"));   // API layer has session context
        }

        [Fact]
        [DisplayName("DbAnomalyEntry is minimal (no who/company) and keeps database_id/command/kind")]
        public void DbAnomalyEntry_LeanColumns()
        {
            var entry = new DbAnomalyEntry
            {
                DatabaseId = "company",
                Command = "UPDATE ft_order SET amount={0}",
                Kind = AnomalyKind.Timeout,
                ElapsedMs = 30000,
                ErrorType = "SqlException",
                ErrorMessage = "timeout expired",
            };

            var map = entry.GetColumns().ToDictionary(c => c.Name, c => c.Value);

            Assert.Equal("st_log_anomaly_db", entry.TableName);
            Assert.Equal("company", map["database_id"]);
            Assert.Equal("UPDATE ft_order SET amount={0}", map["command"]);
            Assert.Equal((int)AnomalyKind.Timeout, map["anomaly_kind"]);
            // Keeps the always-columns.
            Assert.True(map.ContainsKey("sys_rowid"));
            Assert.True(map.ContainsKey("log_time"));
            // Lean: no who / company / source columns (DbAccess has no session).
            Assert.False(map.ContainsKey("user_id"));
            Assert.False(map.ContainsKey("company_id"));
            Assert.False(map.ContainsKey("source"));
        }

        [Fact]
        [DisplayName("DataSet DiffGram serialization keeps the old and new values of modified columns (the core of the changes_xml design)")]
        public void DiffGram_PreservesOldAndNewValues()
        {
            var ds = new DataSet("form");
            var table = ds.Tables.Add("st_test");
            table.Columns.Add("sys_rowid", typeof(string));
            table.Columns.Add("amount", typeof(int));
            var row = table.Rows.Add("r1", 100);
            ds.AcceptChanges();     // baseline: row Unchanged
            row["amount"] = 200;    // modify → old 100 / new 200

            using var changes = ds.GetChanges();
            Assert.NotNull(changes);

            string xml;
            using (var writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                changes!.WriteXml(writer, XmlWriteMode.DiffGram);
                xml = writer.ToString();
            }

            var restored = new DataSet("form");
            var rtable = restored.Tables.Add("st_test");
            rtable.Columns.Add("sys_rowid", typeof(string));
            rtable.Columns.Add("amount", typeof(int));
            using (var reader = new StringReader(xml))
            {
                restored.ReadXml(reader, XmlReadMode.DiffGram);
            }

            var restoredRow = restored.Tables["st_test"]!.Rows[0];
            Assert.Equal(DataRowState.Modified, restoredRow.RowState);
            Assert.Equal(200, restoredRow["amount", DataRowVersion.Current]);
            Assert.Equal(100, restoredRow["amount", DataRowVersion.Original]);
        }

        [Fact]
        [DisplayName("NullAuditLogWriter.Write does not throw")]
        public void NullAuditLogWriter_Write_DoesNotThrow()
        {
            var exception = Record.Exception(() => NullAuditLogWriter.Instance.Write(new TestAuditEntry()));

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("AuditLogOptions defaults: disabled, background writer, access logging disabled")]
        public void AuditLogOptions_Defaults()
        {
            var options = new AuditLogOptions();

            Assert.False(options.Enabled);
            Assert.True(options.UseBackgroundWriter);
            Assert.False(options.AccessEnabled);
        }
    }
}
