using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Transformers;
using Polhem.Core.Data;
using Polhem.Business;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.Definition.Sorting;
using Polhem.JsonRpc.Payload;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Tests.Shared;
using Polhem.Api.Server.UnitTests.Dispatch;

namespace Polhem.Api.Server.UnitTests.Form
{
    /// <summary>
    /// A list request whose filter carries typed values (equality, a range, <c>Between</c>, <c>In</c>) must reach the
    /// database as bindable parameters on every payload format, and its rows must survive the way back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On <c>Plain</c> the filter values are bare JSON. They used to arrive at the data layer as
    /// <c>JsonElement</c>, which no provider can bind, so every such filter failed with an internal server error;
    /// only the <c>LIKE</c> operators worked, because they concatenate the value into a string first. No test covered a
    /// valued filter over <c>Plain</c>, so nothing noticed. On the way back, the client's <c>Plain</c> reader lacked
    /// the <c>DataTable</c> converter, so a result table arrived with no rows and no error.
    /// </para>
    /// <para>
    /// The JSON body codec carries each value's type in its envelope and is covered here as the reference the
    /// <c>Plain</c> path has to match.
    /// </para>
    /// </remarks>
    public class GetListFilterValueDbTests : IClassFixture<SharedDbFixture>
    {
        private const string Qty = "qty";
        private const string Amount = "amount";
        private const string DueDate = "due_date";
        private const string LoggedAt = "logged_at";
        private const string RefUid = "ref_uid";
        private const string Starts = "starts";

        private static readonly Guid s_refB = new("6c1f4c0e-8a0b-4d8e-9a3c-2f5b7d9e1a04");

        private readonly SharedDbFixture _fx;

        public GetListFilterValueDbTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a Plain GetList filtering with In, Between, a range and NotEqual binds its values and returns the matching rows")]
        public Task Plain_Sqlite_RangeAndSetFilters_ReturnMatchingRows() => RunPlainRangeAndSet(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a Plain GetList filtering with In, Between, a range and NotEqual binds its values and returns the matching rows")]
        public Task Plain_SqlServer_RangeAndSetFilters_ReturnMatchingRows() => RunPlainRangeAndSet(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a Plain GetList filtering with In, Between, a range and NotEqual binds its values and returns the matching rows")]
        public Task Plain_PostgreSql_RangeAndSetFilters_ReturnMatchingRows() => RunPlainRangeAndSet(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a Plain GetList filtering with In, Between, a range and NotEqual binds its values and returns the matching rows")]
        public Task Plain_MySql_RangeAndSetFilters_ReturnMatchingRows() => RunPlainRangeAndSet(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a Plain GetList filtering with In, Between, a range and NotEqual binds its values and returns the matching rows")]
        public Task Plain_Oracle_RangeAndSetFilters_ReturnMatchingRows() => RunPlainRangeAndSet(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a Plain GetList filtering with Equal on a text and an integer column, with the default kind and operator omitted, returns the one row")]
        public Task Plain_Sqlite_EqualFilters_ReturnOneRow() => RunPlainEqual(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a Plain GetList filtering with Equal on a text and an integer column, with the default kind and operator omitted, returns the one row")]
        public Task Plain_SqlServer_EqualFilters_ReturnOneRow() => RunPlainEqual(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a Plain GetList filtering a date, a date and time, a Guid and a decimal column with string values returns the matching row")]
        public Task Plain_Sqlite_StringTypedFilters_ReturnMatchingRow() => RunPlainStringTyped(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a Plain GetList filtering a date, a date and time, a Guid and a decimal column with string values returns the matching row")]
        public Task Plain_SqlServer_StringTypedFilters_ReturnMatchingRow() => RunPlainStringTyped(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a Plain GetList filtering a date, a date and time, a Guid and a decimal column with string values returns the matching row")]
        public Task Plain_PostgreSql_StringTypedFilters_ReturnMatchingRow() => RunPlainStringTyped(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a Plain GetList filtering a date, a date and time, a Guid and a decimal column with string values returns the matching row")]
        public Task Plain_MySql_StringTypedFilters_ReturnMatchingRow() => RunPlainStringTyped(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a Plain GetList filtering a date, a date and time, a Guid and a decimal column with string values returns the matching row")]
        public Task Plain_Oracle_StringTypedFilters_ReturnMatchingRow() => RunPlainStringTyped(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a GetList over the JSON body codec filtering with In, Between, a range and NotEqual returns the matching rows")]
        public Task JsonCodec_Sqlite_RangeAndSetFilters_ReturnMatchingRows() => RunJsonCodecRangeAndSet(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a GetList over the JSON body codec filtering with In, Between, a range and NotEqual returns the matching rows")]
        public Task JsonCodec_SqlServer_RangeAndSetFilters_ReturnMatchingRows() => RunJsonCodecRangeAndSet(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a GetList filtering a date column with typed DateOnly values for Equal, Between and In returns the matching rows")]
        public Task JsonCodec_Sqlite_DateOnlyFilters_ReturnMatchingRows() => RunJsonCodecDateOnly(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a GetList filtering a date column with typed DateOnly values for Equal, Between and In returns the matching rows")]
        public Task JsonCodec_SqlServer_DateOnlyFilters_ReturnMatchingRows() => RunJsonCodecDateOnly(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a GetList filtering a date column with typed DateOnly values for Equal, Between and In returns the matching rows")]
        public Task JsonCodec_PostgreSql_DateOnlyFilters_ReturnMatchingRows() => RunJsonCodecDateOnly(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a GetList filtering a date column with typed DateOnly values for Equal, Between and In returns the matching rows")]
        public Task JsonCodec_MySql_DateOnlyFilters_ReturnMatchingRows() => RunJsonCodecDateOnly(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a GetList filtering a date column with typed DateOnly values for Equal, Between and In returns the matching rows")]
        public Task JsonCodec_Oracle_DateOnlyFilters_ReturnMatchingRows() => RunJsonCodecDateOnly(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a Plain GetList whose filter value is not valid for its field answers InvalidParams and names the field")]
        public Task Plain_Sqlite_InvalidFilterValue_ReturnsInvalidParams() => WithSeededForm(DatabaseType.SQLite, async (executor, progId) =>
        {
            const string Body = """{"selectFields":"sys_id","filter":{"fieldName":"due_date","value":"02/01/2026"},"sortFields":[{"fieldName":"sys_id"}]}""";
            var requestJson = $$"""
                {"jsonrpc":"2.0","method":"{{progId}}.{{FormActions.GetList}}","params":{"format":0,"value":{{Body}}},"id":"plain-bad"}
                """;

            var response = await executor.ExecuteJsonAsync(requestJson);

            Assert.NotNull(response.Error);
            Assert.Equal((int)JsonRpcErrorCode.InvalidParams, response.Error!.Code);
            Assert.Contains(DueDate, response.Error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("02/01/2026", response.Error.Message, StringComparison.Ordinal);
        });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a Plain GetList filtering a time of day sent without its leading zero matches the rows stored in the fixed-width form")]
        public Task Plain_Sqlite_UnpaddedTimeOfDay_MatchesStoredRows() => RunPlainTimeOfDay(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a Plain GetList filtering a time of day sent without its leading zero matches the rows stored in the fixed-width form")]
        public Task Plain_SqlServer_UnpaddedTimeOfDay_MatchesStoredRows() => RunPlainTimeOfDay(DatabaseType.SQLServer);

        /// <summary>
        /// The rows store "08:30", "09:00" and "17:30". Compared as sent, "8:00" to "9:00" would be a text range that
        /// excludes "08:30" and "09:00", and "8:30" would match nothing.
        /// </summary>
        private Task RunPlainTimeOfDay(DatabaseType databaseType)
        {
            return WithSeededForm(databaseType, async (executor, progId) =>
            {
                var body = """
                    {
                      "selectFields": "sys_id",
                      "filter": {
                        "kind": "Group",
                        "operator": "Or",
                        "nodes": [
                          { "fieldName": "starts", "operator": "Between", "value": "8:00", "secondValue": "9:00" },
                          { "fieldName": "starts", "value": "5:30" }
                        ]
                      },
                      "sortFields": [{ "fieldName": "sys_id" }]
                    }
                    """;

                var table = await ExecutePlain(executor, progId, body);

                Assert.Equal(["A", "B"], SysIds(table));
            });
        }

        private Task RunPlainRangeAndSet(DatabaseType databaseType)
        {
            return WithSeededForm(databaseType, async (executor, progId) =>
            {
                var body = $$"""
                    {
                      "selectFields": "sys_id,qty,amount",
                      "filter": {
                        "kind": "Group",
                        "nodes": [
                          { "fieldName": "sys_id", "operator": "In", "value": ["A", "B", "C"] },
                          { "fieldName": "qty", "operator": "Between", "value": 2, "secondValue": 3 },
                          { "fieldName": "amount", "operator": "GreaterThanOrEqual", "value": 10.5 },
                          { "fieldName": "sys_id", "operator": "NotEqual", "value": "Z" }
                        ]
                      },
                      "sortFields": [{ "fieldName": "sys_id", "direction": "Asc" }]
                    }
                    """;

                var table = await ExecutePlain(executor, progId, body);

                Assert.Equal(["B", "C"], SysIds(table));
            });
        }

        /// <summary>
        /// A <c>Plain</c> body names no CLR type, so a date, a Guid and a quoted decimal arrive as strings. Bound as
        /// text, PostgreSQL refuses to compare them with the typed column (<c>operator does not exist: date &gt;=
        /// text</c>) and Oracle fails to convert them. SQL Server converted the quoted decimal to the column's own precision,
        /// so <c>'10.5'</c> compared as 11 and the filter returned no rows without an error.
        /// </summary>
        private Task RunPlainStringTyped(DatabaseType databaseType)
        {
            return WithSeededForm(databaseType, async (executor, progId) =>
            {
                var body = $$"""
                    {
                      "selectFields": "sys_id",
                      "filter": {
                        "kind": "Group",
                        "nodes": [
                          { "fieldName": "due_date", "operator": "Between", "value": "2026-01-02", "secondValue": "2026-01-03" },
                          { "fieldName": "logged_at", "operator": "LessThan", "value": "2026-01-02T12:00:00Z" },
                          { "fieldName": "ref_uid", "operator": "In", "value": ["{{s_refB}}", "{{Guid.Empty}}"] },
                          { "fieldName": "amount", "operator": "GreaterThan", "value": "10.5" }
                        ]
                      },
                      "sortFields": [{ "fieldName": "sys_id", "direction": "Asc" }]
                    }
                    """;

                var table = await ExecutePlain(executor, progId, body);

                Assert.Equal(["B"], SysIds(table));
            });
        }

        private Task RunPlainEqual(DatabaseType databaseType)
        {
            return WithSeededForm(databaseType, async (executor, progId) =>
            {
                // The first node leaves out `kind` and `operator`, as the JSON wires do for their default values
                // (Condition and Equal), so this also pins what an absent member means.
                var body = """
                    {
                      "selectFields": "sys_id,qty",
                      "filter": {
                        "kind": "Group",
                        "nodes": [
                          { "fieldName": "sys_id", "value": "B" },
                          { "fieldName": "qty", "operator": "Equal", "value": 2 }
                        ]
                      },
                      "sortFields": [{ "fieldName": "sys_id" }]
                    }
                    """;

                var table = await ExecutePlain(executor, progId, body);

                Assert.Equal(["B"], SysIds(table));
            });
        }

        private Task RunJsonCodecRangeAndSet(DatabaseType databaseType)
        {
            return WithSeededForm(databaseType, async (executor, progId) =>
            {
                var request = new TestRpcRequest
                {
                    Method = $"{progId}.{FormActions.GetList}",
                    Params = new TestPayload
                    {
                        Format = PayloadFormat.Encoded,
                        Codec = PayloadCodecNames.Json,
                        Value = new GetListRequest
                        {
                            SelectFields = "sys_id,qty,amount",
                            Filter = FilterGroup.All(
                                FilterCondition.In(SysFields.Id, ["A", "B", "C"]),
                                FilterCondition.Between(Qty, 2, 3),
                                new FilterCondition(Amount, ComparisonOperator.GreaterThanOrEqual, 10.5m),
                                FilterCondition.NotEqual(SysFields.Id, "Z")),
                            SortFields = [new SortField(SysFields.Id, SortDirection.Asc)],
                        },
                    },
                    Id = Guid.NewGuid().ToString(),
                };
                var response = await executor.ExecuteAsync(request);

                Assert.Null(response.Error);
                var result = Assert.IsType<GetListResponse>(response.Result!.Value);
                Assert.Equal(["B", "C"], SysIds(result.Table!));
            });
        }

        /// <summary>
        /// A <see cref="DateOnly"/> is how a client filters a date column (ADR-032 D4: a <see cref="DateTime"/> filter value
        /// is an instant and moves to UTC). The stored day and the filter's day must compare equal, so <c>Equal</c> finds
        /// row B, <c>Between</c> includes its last day, row C, and each value of <c>In</c> finds its row.
        /// </summary>
        private Task RunJsonCodecDateOnly(DatabaseType databaseType)
        {
            return WithSeededForm(databaseType, async (executor, progId) =>
            {
                var day = new DateOnly(2026, 1, 2);

                Assert.Equal(["B"], await ListAsync(executor, progId, FilterCondition.Equal(DueDate, day)));
                Assert.Equal(["B", "C"], await ListAsync(executor, progId, FilterCondition.Between(DueDate, day, day.AddDays(1))));
                Assert.Equal(["A", "C"], await ListAsync(executor, progId, FilterCondition.In(DueDate, [day.AddDays(-1), day.AddDays(1)])));
            });
        }

        private static async Task<string[]> ListAsync(TestDispatcher executor, string progId, FilterNode filter)
        {
            var request = new TestRpcRequest
            {
                Method = $"{progId}.{FormActions.GetList}",
                Params = new TestPayload
                {
                    Format = PayloadFormat.Encoded,
                    Codec = PayloadCodecNames.Json,
                    Value = new GetListRequest
                    {
                        SelectFields = SysFields.Id,
                        Filter = filter,
                        SortFields = [new SortField(SysFields.Id, SortDirection.Asc)],
                    },
                },
                Id = Guid.NewGuid().ToString(),
            };
            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            return SysIds(Assert.IsType<GetListResponse>(response.Result!.Value).Table!);
        }

        /// <summary>
        /// Runs a <c>Plain</c> request the way it travels: the body is parsed from JSON as the HTTP endpoint parses it,
        /// and the response is written to JSON and read back as the client reads it.
        /// </summary>
        private static async Task<DataTable> ExecutePlain(TestDispatcher executor, string progId, string body)
        {
            var requestJson = $$"""
                {"jsonrpc":"2.0","method":"{{progId}}.{{FormActions.GetList}}","params":{"format":0,"value":{{body}}},"id":"plain-1"}
                """;
            var response = await executor.ExecuteJsonAsync(requestJson);

            Assert.Null(response.Error);
            var result = Assert.IsType<GetListResponse>(response.Result!.Value);
            Assert.NotNull(result.Table);
            return result.Table!;
        }

        private static string[] SysIds(DataTable table)
            => table.Rows.Cast<DataRow>().Select(r => (string)r[SysFields.Id]).ToArray();

        /// <summary>
        /// Creates a transient form with a text, an integer and a decimal column, seeds three rows, and hands the test
        /// a dispatcher whose business objects read that form.
        /// </summary>
        private async Task WithSeededForm(DatabaseType databaseType, Func<TestDispatcher, string, Task> test)
        {
            string progId = TransientForm.NewTableName("tb_glf_");
            var schema = new FormSchema(progId, "Filter values") { CategoryId = TransientForm.CategoryId };
            var table = schema.Tables!.Add(progId, "Filter values");
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields.Add(SysFields.Id, "Code", FieldDbType.String).MaxLength = 20;
            table.Fields.Add(Qty, "Quantity", FieldDbType.Integer);
            table.Fields.Add(Amount, "Amount", FieldDbType.Decimal);
            table.Fields.Add(DueDate, "Due date", FieldDbType.Date);
            table.Fields.Add(LoggedAt, "Logged at", FieldDbType.DateTime);
            table.Fields.Add(RefUid, "Reference", FieldDbType.Guid);
            table.Fields.Add(Starts, "Starts", FieldDbType.Time);

            var form = new TransientForm(_fx, databaseType, schema);
            form.CreateTables();
            try
            {
                // Whole amounts: the generated column has the default scale of 0, and the point here is that a
                // fractional filter value binds, not how each provider rounds a stored fraction.
                // B is the only row inside every string-typed filter of `RunPlainStringTyped`; A and C each fall
                // outside more than one, so a filter that is silently ignored still changes the result.
                Insert(form, progId, "A", 1, 5m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified), Guid.NewGuid(), "08:30");
                Insert(form, progId, "B", 2, 11m, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Unspecified), s_refB, "09:00");
                Insert(form, progId, "C", 3, 20m, new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 3, 9, 0, 0, DateTimeKind.Unspecified), Guid.NewGuid(), "17:30");

                var services = new TestOverrideServiceProvider(
                    _fx.Provider,
                    (typeof(IRepositoryFactory), new FormRepositoryFactory(form.Repository)));
                var boFactory = new BusinessObjectFactory(
                    services,
                    form.DefineAccess,
                    _fx.GetRequiredService<ISessionInfoService>(),
                    _fx.GetRequiredService<ILanguageService>(),
                    new FormBoTypeResolver());

                var executor = new TestDispatcher(_fx.Provider, boFactory)
                {
                    AccessToken = TestSessionFactory.CreateAccessToken(_fx),
                    IsLocalCall = true,
                };

                await test(executor, progId);
            }
            finally
            {
                form.DropTables();
            }
        }

        private static void Insert(TransientForm form, string progId, string sysId, int qty, decimal amount,
            DateTime dueDate, DateTime loggedAt, Guid refUid, string starts)
        {
            form.DbAccess.ExecuteNonQuery(
                $"INSERT INTO {form.Quote(progId)} ({form.Quote(SysFields.RowId)}, {form.Quote(SysFields.Id)}, {form.Quote(Qty)}, {form.Quote(Amount)}, " +
                $"{form.Quote(DueDate)}, {form.Quote(LoggedAt)}, {form.Quote(RefUid)}, {form.Quote(Starts)}, {form.Quote(SysFields.CompanyId)}) " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})",
                Guid.NewGuid(), sysId, qty, amount, dueDate, loggedAt, refUid, starts, TransientForm.CompanyId);
        }

        private sealed class FormRepositoryFactory(IDataFormRepository repository) : IRepositoryFactory
        {
            public T CreateFormRepository<T>(Guid accessToken, string progId, SessionCompanyScope companyScope) where T : class, IDataFormRepository => CreateFormRepository<T>(accessToken, progId);

            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository
                => (T)repository;

            public T Create<T>(Guid accessToken = default) where T : class => throw new NotSupportedException();
        }

        private sealed class FormBoTypeResolver : IBoTypeResolver
        {
            public Type Resolve(string progId) => typeof(FormBusinessObject);
        }
    }
}
