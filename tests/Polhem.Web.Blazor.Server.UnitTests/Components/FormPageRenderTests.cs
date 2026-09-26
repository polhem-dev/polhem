using System.ComponentModel;
using System.Reflection;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DataObjects;
using Microsoft.AspNetCore.Components.Rendering;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers the Razor template <c>BuildRenderTree</c> of <see cref="FormPage"/>.
    /// The tests set private fields through reflection and call <c>BuildRenderTree</c> without DI,
    /// covering the template branches for error, loading, and the full toolbar (IsDirty false and true).
    /// </summary>
    public class FormPageRenderTests
    {
        private static readonly MethodInfo s_buildRenderTree =
            typeof(FormPage)
                .GetMethod("BuildRenderTree", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly FieldInfo s_errorField =
            typeof(FormPage).GetField("_error", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly FieldInfo s_isInitializingField =
            typeof(FormPage).GetField("_isInitializing", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly FieldInfo s_dataObjectField =
            typeof(FormPage).GetField("_dataObject", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static void Render(FormPage page)
        {
            var builder = new RenderTreeBuilder();
            s_buildRenderTree.Invoke(page, new object[] { builder });
        }

        private static FormDataObject CreateFreshDataObject()
        {
            var schema = new FormSchema("Test", "Test");
            schema.Tables!.Add("Test", "Test");
            return new FormDataObject(schema);
        }

        private static FormDataObject CreateDirtyDataObject()
        {
            var schema = new FormSchema("Test", "Test");
            var masterTable = schema.Tables!.Add("Test", "Test");
            masterTable.Fields!.Add("name", "名稱", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.SetField("name", "dirty-value");
            return dataObject;
        }

        [Fact]
        [DisplayName("BuildRenderTree renders the error block without throwing when there is an error message")]
        public void BuildRenderTree_WithError_DoesNotThrow()
        {
            var page = new FormPage();
            s_errorField.SetValue(page, "初始化失敗");
            s_isInitializingField.SetValue(page, false);
            var ex = Record.Exception(() => Render(page));
            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("BuildRenderTree renders the loading block without throwing while initialization is in progress")]
        public void BuildRenderTree_Initializing_DoesNotThrow()
        {
            var page = new FormPage();
            var ex = Record.Exception(() => Render(page));
            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("BuildRenderTree renders the loading block without throwing when DataObject is null after initialization")]
        public void BuildRenderTree_DataObjectNull_DoesNotThrow()
        {
            var page = new FormPage();
            s_isInitializingField.SetValue(page, false);
            var ex = Record.Exception(() => Render(page));
            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("BuildRenderTree renders the full toolbar without throwing when DataObject is set and IsDirty is false")]
        public void BuildRenderTree_DataObjectSet_NotDirty_DoesNotThrow()
        {
            var page = new FormPage();
            s_isInitializingField.SetValue(page, false);
            s_dataObjectField.SetValue(page, CreateFreshDataObject());
            var ex = Record.Exception(() => Render(page));
            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("BuildRenderTree renders the unsaved hint without throwing when DataObject.IsDirty is true")]
        public void BuildRenderTree_DataObjectDirty_DoesNotThrow()
        {
            var page = new FormPage();
            s_isInitializingField.SetValue(page, false);
            s_dataObjectField.SetValue(page, CreateDirtyDataObject());
            var ex = Record.Exception(() => Render(page));
            Assert.Null(ex);
        }
    }
}
