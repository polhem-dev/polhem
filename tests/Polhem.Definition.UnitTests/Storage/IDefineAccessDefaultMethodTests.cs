using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Storage
{
    /// <summary>
    /// Verifies the delegation of the default interface methods (DIM) of <see cref="IDefineAccess"/>:
    /// <c>GetPermissionModels</c>, <c>SavePermissionModels</c> and
    /// <c>GetFormLayout(customizeId, layoutId)</c>.
    /// They are called through the <see cref="MinimalDefineAccess"/> stub (which does not override them)
    /// to confirm that the default path actually runs.
    /// </summary>
    public class IDefineAccessDefaultMethodTests
    {
        private sealed class MinimalDefineAccess : IDefineAccess
        {
            public DefineType? LastSavedDefineType { get; private set; }
            public string? LastGetFormLayoutId { get; private set; }

            public object GetDefine(DefineType defineType, string[]? keys = null)
                => defineType == DefineType.PermissionModels
                    ? new PermissionModels()
                    : throw new NotImplementedException();

            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null)
                => LastSavedDefineType = defineType;

            public SystemSettings GetSystemSettings() => throw new NotImplementedException();
            public void SaveSystemSettings(SystemSettings settings) => throw new NotImplementedException();
            public DatabaseSettings GetDatabaseSettings() => throw new NotImplementedException();
            public void SaveDatabaseSettings(DatabaseSettings settings) => throw new NotImplementedException();
            public ProgramSettings GetProgramSettings() => throw new NotImplementedException();
            public void SaveProgramSettings(ProgramSettings settings) => throw new NotImplementedException();
            public DbCategorySettings GetDbCategorySettings() => throw new NotImplementedException();
            public void SaveDbCategorySettings(DbCategorySettings settings) => throw new NotImplementedException();
            public TableSchema GetTableSchema(string categoryId, string tableName) => throw new NotImplementedException();
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) => throw new NotImplementedException();
            public FormSchema GetFormSchema(string progId) => throw new NotImplementedException();
            public void SaveFormSchema(FormSchema formSchema) => throw new NotImplementedException();
            public FormLayout GetFormLayout(string layoutId)
            {
                LastGetFormLayoutId = layoutId;
                return new FormLayout();
            }
            public void SaveFormLayout(FormLayout formLayout) => throw new NotImplementedException();
            public LanguageResource GetLanguage(string lang, string ns) => throw new NotImplementedException();
            public void SaveLanguage(LanguageResource resource) => throw new NotImplementedException();
        }

        [Fact]
        [DisplayName("GetPermissionModels default implementation delegates to GetDefine(DefineType.PermissionModels) and returns PermissionModels")]
        public void GetPermissionModels_DefaultImpl_DelegatesToGetDefine()
        {
            IDefineAccess access = new MinimalDefineAccess();
            var result = access.GetPermissionModels();
            Assert.NotNull(result);
            Assert.IsType<PermissionModels>(result);
        }

        [Fact]
        [DisplayName("SavePermissionModels default implementation delegates to SaveDefine(DefineType.PermissionModels, ...)")]
        public void SavePermissionModels_DefaultImpl_DelegatesToSaveDefine()
        {
            var stub = new MinimalDefineAccess();
            IDefineAccess access = stub;
            access.SavePermissionModels(new PermissionModels());
            Assert.Equal(DefineType.PermissionModels, stub.LastSavedDefineType);
        }

        [Fact]
        [DisplayName("GetFormLayout(customizeId, layoutId) default implementation ignores customizeId and delegates to GetFormLayout(layoutId)")]
        public void GetFormLayout_WithCustomizeId_DefaultImpl_DelegatesToSingleParam()
        {
            var stub = new MinimalDefineAccess();
            IDefineAccess access = stub;
            var result = access.GetFormLayout("any_customize", "TestLayout");
            Assert.NotNull(result);
            Assert.Equal("TestLayout", stub.LastGetFormLayoutId);
        }
    }
}
