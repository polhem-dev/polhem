using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Messages.System;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Paging;
using Polhem.Definition.Settings;
using Polhem.UI.Core;
using Polhem.Web.Blazor.Components;
using Polhem.Web.Blazor.Identity;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// Stands in for the backend of two forms, <c>Emp</c> and the <c>Dept</c> its <c>dept_rowid</c> relates to, for the
    /// shared form page tests and the browser tests. It records what each call asked for.
    /// </summary>
    public sealed class FormPageTestBackend
    {
        public const string EmpProgId = "Emp";
        public const string DeptProgId = "Dept";
        public const string DeptModelId = "DeptModel";
        public const string EmpModelId = "EmpModel";
        public const string OtherProgId = "Other";
        public const string RelationField = "dept_rowid";
        public const string ParentField = "parent_rowid";
        public static readonly Guid AliceId = new("aaaaaaaa-0000-0000-0000-000000000001");
        public static readonly Guid BobId = new("aaaaaaaa-0000-0000-0000-000000000002");
        public static readonly Guid DeptId = new("dddddddd-0000-0000-0000-000000000001");
        public static readonly Guid ParentDeptId = new("dddddddd-0000-0000-0000-000000000002");

        private readonly FormSchema _emp;
        private readonly FormSchema _dept;

        /// <summary>
        /// Initializes a new instance of <see cref="FormPageTestBackend"/>.
        /// </summary>
        /// <param name="permissionBound">Whether both forms check capabilities.</param>
        /// <param name="nameRequired">Whether <c>emp_name</c> is required.</param>
        /// <param name="amountExpression">The expression of a computed field <c>amount</c>, or <c>null</c> for none.</param>
        public FormPageTestBackend(bool permissionBound = false, bool nameRequired = false, string? amountExpression = null)
        {
            _emp = BuildEmpSchema(permissionBound, nameRequired, amountExpression);
            _dept = BuildDeptSchema(permissionBound);
            Server = new FakeApiServer()
                .On<GetListRequest>($"{EmpProgId}.{FormActions.GetList}", GetList)
                .On<GetDataRequest>($"{EmpProgId}.{FormActions.GetData}", GetEmp)
                .On<GetNewDataRequest>($"{EmpProgId}.{FormActions.GetNewData}", _ => new GetNewDataResponse { DataSet = Record(Guid.NewGuid(), string.Empty, accept: false) })
                .On<GetListRequest>($"{OtherProgId}.{FormActions.GetList}", _ => OtherList())
                .On<SaveRequest>($"{EmpProgId}.{FormActions.Save}", Save)
                .On<DeleteRequest>($"{EmpProgId}.{FormActions.Delete}", Delete)
                .On<GetDataRequest>($"{DeptProgId}.{FormActions.GetData}", request =>
                {
                    DeptLoaded.Add(request.RowId);
                    return new GetDataResponse { DataSet = DeptRecord(request.RowId) };
                })
                .On<GetDefineRequest>($"{SysProgIds.System}.{SystemActions.GetDefine}", GetDefine)
                .On<GetFormLayoutRequest>($"{SysProgIds.System}.{SystemActions.GetCustomizeFormLayout}", _ => new GetFormLayoutResponse())
                .On<GetLanguageRequest>($"{SysProgIds.System}.{SystemActions.GetCustomizeLanguage}", _ => new GetLanguageResponse())
                .On<EnterCompanyRequest>($"{SysProgIds.System}.{SystemActions.EnterCompany}", request => new EnterCompanyResponse
                {
                    Company = new CompanyInfo { CompanyId = request.CompanyId },
                    Capabilities = new Dictionary<string, PermissionActions>(Capabilities, StringComparer.Ordinal),
                });
        }

        public FakeApiServer Server { get; }

        public int GetListCount { get; private set; }

        public List<PagingOptions> ListPaging { get; } = [];

        public string? LastSortField { get; private set; }

        public int? TotalRows { get; set; }

        /// <summary>Gets or sets whether each page holds rows of its own, as a real list does, rather than Alice and Bob.</summary>
        public bool DistinctRows { get; set; }

        public int SaveCount { get; private set; }

        public List<Guid> Deleted { get; } = [];

        public string? GetDataFailure { get; set; }

        public string? SaveFailure { get; set; }

        public bool MissingLayout { get; set; }

        public bool EmptyRelation { get; set; }

        public List<Guid> DeptLoaded { get; } = [];

        public List<string> ListSelectFields { get; } = [];

        public LanguageResource? Translation { get; set; }

        public int LanguageFetchCount { get; private set; }

        public bool UseDefinitionLoader { get; set; } = true;

        public bool MissingSchema { get; set; }

        public Dictionary<string, PermissionActions> Capabilities { get; } = new(StringComparer.Ordinal);

        /// <summary>Registers what a form page needs, with its client calling this backend.</summary>
        /// <param name="services">The service collection.</param>
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<IBlazorDefinitionOptions>(new Options(UseDefinitionLoader));
            services.AddScoped(_ => Server.CreateClient());
            services.AddScoped(sp => new BlazorUserContext(sp.GetRequiredService<Polhem.Api.Client.PolhemApiClient>(), string.Empty));
            services.AddScoped<IUserContext>(sp => sp.GetRequiredService<BlazorUserContext>());
        }

        private static FormSchema BuildEmpSchema(bool permissionBound, bool nameRequired, string? amountExpression)
        {
            var schema = new FormSchema(EmpProgId, "Employee") { ListFields = "emp_name" };
            if (permissionBound) schema.PermissionModelId = EmpModelId;
            var fields = schema.Tables!.Add(EmpProgId, "Employee").Fields!;
            fields.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            fields.Add("emp_name", "Name", FieldDbType.String).Required = nameRequired;
            var dept = fields.Add(RelationField, "Department", FieldDbType.Guid);
            dept.RelationProgId = DeptProgId;
            dept.RelationFieldMappings!.Add("sys_name", "ref_dept_name");
            fields.Add("ref_dept_name", "Department name", FieldDbType.String);
            if (permissionBound)
                fields.Add(new FormField("salary", "Salary", FieldDbType.Decimal) { SensitiveCategory = SensitiveCategory.Amount });
            if (amountExpression is not null)
            {
                fields.Add(new FormField("price", "Price", FieldDbType.Decimal) { NumberKind = NumberKind.UnitPrice });
                fields.Add(new FormField("qty", "Qty", FieldDbType.Decimal) { NumberKind = NumberKind.Quantity });
                fields.Add(new FormField("amount", "Amount", FieldDbType.Decimal)
                {
                    NumberKind = NumberKind.Amount,
                    ValueExpression = amountExpression,
                    ReadOnly = true,
                });
            }
            return schema;
        }

        private static FormSchema BuildDeptSchema(bool permissionBound)
        {
            var schema = new FormSchema(DeptProgId, "Department");
            if (permissionBound) schema.PermissionModelId = DeptModelId;
            var fields = schema.Tables!.Add(DeptProgId, "Department").Fields!;
            fields.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            fields.Add("sys_name", "Name", FieldDbType.String);
            var parent = fields.Add(ParentField, "Parent", FieldDbType.Guid);
            parent.RelationProgId = DeptProgId;
            parent.RelationFieldMappings!.Add("sys_name", "ref_parent_name");
            fields.Add("ref_parent_name", "Parent name", FieldDbType.String);
            return schema;
        }

        private GetListResponse GetList(GetListRequest request)
        {
            GetListCount++;
            ListSelectFields.Add(request.SelectFields);
            if (request.Paging is { } paging) ListPaging.Add(paging);
            LastSortField = request.SortFields is { Count: > 0 } sort ? sort[0].FieldName : null;
            var table = new DataTable(EmpProgId);
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add("emp_name", typeof(string));
            if (DistinctRows && request.Paging is { } page && TotalRows is { } rows)
            {
                int first = (page.Page - 1) * page.PageSize;
                foreach (var i in Enumerable.Range(first, Math.Max(0, Math.Min(page.PageSize, rows - first))))
                    table.Rows.Add(new Guid(i, 0, 0, new byte[8]), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Row {i}"));
            }
            else
            {
                table.Rows.Add(AliceId, "Alice");
                table.Rows.Add(BobId, "Bob");
            }
            return new GetListResponse
            {
                Table = table,
                Paging = TotalRows is { } total && request.Paging is { } options
                    ? new PagingInfo
                    {
                        Page = options.Page,
                        PageSize = options.PageSize,
                        TotalCount = options.IncludeTotalCount ? total : null,
                        HasMore = options.Page * options.PageSize < total,
                    }
                    : null,
            };
        }

        private GetDataResponse GetEmp(GetDataRequest request)
        {
            if (GetDataFailure is not null) throw new UserMessageException(GetDataFailure);
            return new GetDataResponse { DataSet = Record(request.RowId, request.RowId == AliceId ? "Alice" : "Bob") };
        }

        private SaveResponse Save(SaveRequest request)
        {
            if (SaveFailure is not null) throw new UserMessageException(SaveFailure);
            SaveCount++;
            return new SaveResponse { DataSet = request.DataSet };
        }

        private DeleteResponse Delete(DeleteRequest request)
        {
            Deleted.Add(request.RowId);
            return new DeleteResponse { RowsAffected = 1 };
        }

        private GetDefineResponse GetDefine(GetDefineRequest request)
        {
            string progId = request.Keys is { Length: > 0 } keys ? keys[0] : string.Empty;
            FormSchema schema = progId == DeptProgId ? _dept : _emp;
            object? stored = request.DefineType switch
            {
                DefineType.FormSchema => MissingSchema ? null : schema,
                DefineType.FormLayout => MissingLayout ? null : FormLayoutGenerator.Generate(schema, progId),
                DefineType.Language => Language(request.Keys),
                DefineType.CurrencySettings or DefineType.UnitSettings => null,
                _ => throw new NotSupportedException($"GetDefine for {request.DefineType} is not supported by the fake."),
            };
            return new GetDefineResponse { Xml = stored is null ? string.Empty : XmlCodec.Serialize(stored) };
        }

        private LanguageResource? Language(string[]? keys)
        {
            LanguageFetchCount++;
            return Translation is { } translation && keys is [var lang, var ns] && lang == translation.Lang && ns == translation.Namespace
                ? translation
                : null;
        }

        private static GetListResponse OtherList()
        {
            var table = new DataTable(OtherProgId);
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add("emp_name", typeof(string));
            table.Rows.Add(Guid.NewGuid(), "Carol");
            return new GetListResponse { Table = table };
        }

        private DataSet Record(Guid rowId, string name, bool accept = true)
        {
            var dataSet = FormValueBinding.BuildEmptyDataSet(_emp);
            var table = dataSet.Tables[EmpProgId]!;
            var row = table.NewRow();
            row[SysFields.RowId] = rowId;
            row["emp_name"] = name;
            if (!EmptyRelation)
            {
                row[RelationField] = DeptId;
                row["ref_dept_name"] = "Engineering";
            }
            table.Rows.Add(row);
            if (accept) dataSet.AcceptChanges();
            return dataSet;
        }

        private DataSet DeptRecord(Guid rowId)
        {
            var dataSet = FormValueBinding.BuildEmptyDataSet(_dept);
            var table = dataSet.Tables[DeptProgId]!;
            var row = table.NewRow();
            row[SysFields.RowId] = rowId;
            row["sys_name"] = rowId == DeptId ? "Engineering" : "Company";
            if (rowId == DeptId)
            {
                row[ParentField] = ParentDeptId;
                row["ref_parent_name"] = "Company";
            }
            table.Rows.Add(row);
            dataSet.AcceptChanges();
            return dataSet;
        }

        private sealed class Options(bool useDefinitionLoader) : IBlazorDefinitionOptions
        {
            public string DefaultLanguage => string.Empty;

            public bool UseDefinitionLoader => useDefinitionLoader;
        }
    }
}
