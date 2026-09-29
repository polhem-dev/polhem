using System.ComponentModel;
using System.Data;
using System.Text.Json;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Transformers;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Business;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Definition.Sorting;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.Form
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
    [Collection(ApiServiceOptionsStateCollection.Name)]
    public class GetListFilterValueDbTests : IClassFixture<SharedDbFixture>
    {
        private const string Qty = "qty";
        private const string Amount = "amount";

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
        [DisplayName("SQLite: a GetList over the JSON body codec filtering with In, Between, a range and NotEqual returns the matching rows")]
        public Task JsonCodec_Sqlite_RangeAndSetFilters_ReturnMatchingRows() => RunJsonCodecRangeAndSet(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a GetList over the JSON body codec filtering with In, Between, a range and NotEqual returns the matching rows")]
        public Task JsonCodec_SqlServer_RangeAndSetFilters_ReturnMatchingRows() => RunJsonCodecRangeAndSet(DatabaseType.SQLServer);

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
                var compressor = ApiServiceOptions.PayloadCompressor;
                var encryptor = ApiServiceOptions.PayloadEncryptor;
                ApiServiceOptions.Initialize(
                    new ApiPayloadOptions { Compressor = "gzip", Encryptor = "aes-cbc-hmac" },
                    isDebugMode: true);
                try
                {
                    var request = new JsonRpcRequest
                    {
                        Method = $"{progId}.{FormActions.GetList}",
                        Params = new JsonRpcParams
                        {
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
                    ApiPayloadConverter.TransformTo(request.Params, PayloadFormat.Encoded);

                    var response = await executor.ExecuteAsync(request);

                    Assert.Null(response.Error);
                    ApiPayloadConverter.RestoreFrom(response.Result!, PayloadFormat.Encoded);
                    var result = Assert.IsType<GetListResponse>(response.Result!.Value);
                    Assert.Equal(["B", "C"], SysIds(result.Table!));
                }
                finally
                {
                    ApiServiceOptions.Initialize(compressor, encryptor);
                }
            });
        }

        /// <summary>
        /// Runs a <c>Plain</c> request the way it travels: the body is parsed from JSON as the controller parses it,
        /// and the response is written to JSON and read back as the client reads it.
        /// </summary>
        private static async Task<DataTable> ExecutePlain(JsonRpcExecutor executor, string progId, string body)
        {
            var requestJson = $$"""
                {"jsonrpc":"2.0","method":"{{progId}}.{{FormActions.GetList}}","params":{"format":0,"value":{{body}}},"id":"plain-1"}
                """;
            var request = JsonCodec.Deserialize<JsonRpcRequest>(requestJson)!;

            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            var received = JsonCodec.Deserialize<JsonRpcResponse>(JsonCodec.Serialize(response))!;
            var element = Assert.IsType<JsonElement>(received.Result!.Value);
            var result = ApiOutputConverter.ConvertResultValue<GetListResponse>(element)!;
            Assert.NotNull(result.Table);
            return result.Table!;
        }

        private static string[] SysIds(DataTable table)
            => table.Rows.Cast<DataRow>().Select(r => (string)r[SysFields.Id]).ToArray();

        /// <summary>
        /// Creates a transient form with a text, an integer and a decimal column, seeds three rows, and hands the test
        /// an executor whose business objects read that form.
        /// </summary>
        private async Task WithSeededForm(DatabaseType databaseType, Func<JsonRpcExecutor, string, Task> test)
        {
            string progId = TransientForm.NewTableName("tb_glf_");
            var schema = new FormSchema(progId, "Filter values") { CategoryId = TransientForm.CategoryId };
            var table = schema.Tables!.Add(progId, "Filter values");
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields.Add(SysFields.Id, "Code", FieldDbType.String).MaxLength = 20;
            table.Fields.Add(Qty, "Quantity", FieldDbType.Integer);
            table.Fields.Add(Amount, "Amount", FieldDbType.Decimal);

            var form = new TransientForm(_fx, databaseType, schema);
            form.CreateTables();
            try
            {
                // Whole amounts: the generated column has the default scale of 0, and the point here is that a
                // fractional filter value binds, not how each provider rounds a stored fraction.
                Insert(form, progId, "A", 1, 5m);
                Insert(form, progId, "B", 2, 11m);
                Insert(form, progId, "C", 3, 20m);

                var services = new TestOverrideServiceProvider(
                    _fx.Provider,
                    (typeof(IRepositoryFactory), new FormRepositoryFactory(form.Repository)));
                var boFactory = new BusinessObjectFactory(
                    services,
                    form.DefineAccess,
                    _fx.GetRequiredService<ISessionInfoService>(),
                    _fx.GetRequiredService<ILanguageService>(),
                    new FormBoTypeResolver());

                var executor = new JsonRpcExecutor(
                    boFactory,
                    _fx.GetRequiredService<IAccessTokenValidator>(),
                    _fx.GetRequiredService<IApiEncryptionKeyProvider>())
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

        private static void Insert(TransientForm form, string progId, string sysId, int qty, decimal amount)
        {
            form.DbAccess.ExecuteNonQuery(
                $"INSERT INTO {form.Quote(progId)} ({form.Quote(SysFields.RowId)}, {form.Quote(SysFields.Id)}, {form.Quote(Qty)}, {form.Quote(Amount)}) " +
                "VALUES ({0}, {1}, {2}, {3})",
                Guid.NewGuid(), sysId, qty, amount);
        }

        private sealed class FormRepositoryFactory(IDataFormRepository repository) : IRepositoryFactory
        {
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
