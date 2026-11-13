using System.ComponentModel;
using System.Data;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// The base <see cref="FormBusinessPlugin"/>: its constructor guards, the company scope it reads when the context
    /// carries none, and the stage hooks that do nothing until overridden.
    /// </summary>
    public class FormBusinessPluginBaseTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public FormBusinessPluginBaseTests(PolhemTestFixture fx) { _fx = fx; }

        private sealed class ProbePlugin : FormBusinessPlugin
        {
            public ProbePlugin(IBusinessObjectContext ctx, Guid accessToken, string progId) : base(ctx, accessToken, progId) { }

            public SessionCompanyScope Scope => CompanyScope;
        }

        private BusinessObjectContext Context() => new()
        {
            DefineAccess = _fx.GetRequiredService<IDefineAccess>(),
            SessionInfoService = _fx.GetRequiredService<ISessionInfoService>(),
            LanguageService = _fx.GetRequiredService<ILanguageService>(),
            BoFactory = _fx.GetRequiredService<IBusinessObjectFactory>(),
            Services = _fx.Provider,
        };

        [Fact]
        [DisplayName("The plugin constructor refuses a null context or a null progId")]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ProbePlugin(null!, Guid.Empty, "P"));
            Assert.Throws<ArgumentNullException>(() => new ProbePlugin(Context(), Guid.Empty, null!));
        }

        [Fact]
        [DisplayName("Without a token or a context scope the plugin acts outside any company")]
        public void CompanyScope_EmptyToken_IsNone()
        {
            Assert.Same(SessionCompanyScope.None, new ProbePlugin(Context(), Guid.Empty, "P").Scope);
        }

        [Fact]
        [DisplayName("Without a context scope the plugin reads the company its session has entered")]
        public void CompanyScope_SessionInCompany_ReadsSessionScope()
        {
            var token = TestSessionFactory.CreateAccessToken(_fx, companyId: "plg1");

            Assert.Equal("plg1", new ProbePlugin(Context(), token, "P").Scope.CompanyId);
        }

        [Fact]
        [DisplayName("The base stage hooks leave the data untouched")]
        public void StageHooks_Base_DoNothing()
        {
            var plugin = new ProbePlugin(Context(), Guid.Empty, "P");
            var schema = new FormSchema("P", "P");
            var dataSet = new DataSet();
            var repo = new StubFormRepository();
            var save = new SaveContext(new SaveArgs { DataSet = dataSet }, dataSet, repo, schema);
            var delete = new DeleteContext(new DeleteArgs { RowId = Guid.NewGuid() }, repo, null, schema);

            plugin.BeforeSave(save);
            plugin.AfterSave(save);
            plugin.BeforeDelete(delete);
            plugin.AfterDelete(delete);

            Assert.Empty(dataSet.Tables);
            Assert.Empty(repo.Saved);
        }
    }
}
