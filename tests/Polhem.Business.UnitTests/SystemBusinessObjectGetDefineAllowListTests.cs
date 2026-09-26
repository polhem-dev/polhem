using System.ComponentModel;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// <see cref="SystemBusinessObject.GetDefine"/> serves a remote caller only the definition types on its allow-list;
    /// a local call reads every type.
    /// </summary>
    /// <remarks>
    /// The check used to be a deny-list of three server settings, so every other type — table schemas, database
    /// categories, permission models, plugin bindings, and any type added later — was remotely readable by default.
    /// </remarks>
    public class SystemBusinessObjectGetDefineAllowListTests : IClassFixture<PolhemTestFixture>
    {
        private static readonly string[] s_departmentKeys = ["Department"];

        private readonly PolhemTestFixture _fx;

        public SystemBusinessObjectGetDefineAllowListTests(PolhemTestFixture fx) { _fx = fx; }

        [Theory]
        [InlineData(DefineType.SystemSettings)]
        [InlineData(DefineType.DatabaseSettings)]
        [InlineData(DefineType.ProgramSettings)]
        [InlineData(DefineType.DbCategorySettings)]
        [InlineData(DefineType.TableSchema)]
        [InlineData(DefineType.PermissionModels)]
        [InlineData(DefineType.PluginSettings)]
        [InlineData((DefineType)999)]
        [DisplayName("A remote GetDefine for a type outside the allow-list throws NotSupportedException")]
        public void GetDefine_RemoteTypeOutsideAllowList_Throws(DefineType defineType)
        {
            var bo = Bo(isLocalCall: false);

            Assert.Throws<NotSupportedException>(() => bo.GetDefine(new GetDefineArgs { DefineType = defineType }));
        }

        [Fact]
        [DisplayName("A remote GetDefine for FormSchema, which the shipped clients render from, returns its XML")]
        public void GetDefine_RemoteFormSchema_ReturnsXml()
        {
            var result = Bo(isLocalCall: false).GetDefine(
                new GetDefineArgs { DefineType = DefineType.FormSchema, Keys = s_departmentKeys });

            Assert.False(string.IsNullOrWhiteSpace(result.Xml));
        }

        [Fact]
        [DisplayName("A local GetDefine still reads a type outside the remote allow-list")]
        public void GetDefine_LocalTypeOutsideAllowList_ReturnsXml()
        {
            var result = Bo(isLocalCall: true).GetDefine(new GetDefineArgs { DefineType = DefineType.DbCategorySettings });

            Assert.False(string.IsNullOrWhiteSpace(result.Xml));
        }

        private SystemBusinessObject Bo(bool isLocalCall)
            => new(TestPolhemContext.Create(_fx), Guid.Empty, SysProgIds.System, isLocalCall);
    }
}
