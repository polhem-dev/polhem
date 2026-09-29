using System.ComponentModel;
using Polhem.Core.Exceptions;
using Polhem.Core.Serialization;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Behavior tests of <see cref="SystemBusinessObject.GetCustomizeFormLayout"/> and
    /// <see cref="SystemBusinessObject.GetCustomizeLanguage"/>: the tenant is always the session's customization code,
    /// never a value the caller supplies, and a session without one gets an empty answer without the reader being asked.
    /// </summary>
    public class SystemBusinessObjectCustomizeDefineTests : IClassFixture<PolhemTestFixture>
    {
        private const string TenantId = "TENANT_A";

        private readonly PolhemTestFixture _fx;

        public SystemBusinessObjectCustomizeDefineTests(PolhemTestFixture fx) { _fx = fx; }

        /// <summary>Records every lookup and answers with an override whose identity echoes the request.</summary>
        private sealed class RecordingCustomizeReader : ICustomizeDefineReader
        {
            public bool HasOverride { get; init; } = true;

            public List<string> Calls { get; } = [];

            public FormLayout? GetCustomizeFormLayout(string customizeId, string layoutId)
            {
                Calls.Add($"FormLayout:{customizeId}:{layoutId}");
                return HasOverride ? new FormLayout { LayoutId = layoutId, Caption = customizeId } : null;
            }

            public LanguageResource? GetCustomizeLanguage(string customizeId, string lang, string ns)
            {
                Calls.Add($"Language:{customizeId}:{lang}:{ns}");
                return HasOverride ? new LanguageResource { Lang = lang, Namespace = ns } : null;
            }

            public ProgramSettings? GetCustomizeProgramSettings(string customizeId) => null;

            public MenuSettings? GetCustomizeMenuSettings(string customizeId) => null;

            public PluginSettings? GetCustomizePluginSettings(string customizeId) => null;
        }

        private Guid CreateSession(string customizeId)
        {
            var token = TestSessionFactory.CreateAccessToken(_fx);
            var sessions = _fx.GetRequiredService<ISessionInfoService>();
            var session = sessions.Get(token)!;
            session.CompanyScope = new SessionCompanyScope("C001", customizeId, [], Guid.Empty, Guid.Empty, Guid.Empty);
            sessions.Set(session);
            return token;
        }

        private SystemBusinessObject CreateBo(Guid token, ICustomizeDefineReader reader)
            => new(TestBusinessObjectContext.CreateWithOverrides(_fx, (typeof(ICustomizeDefineReader), reader)),
                token, SysProgIds.System);

        [Fact]
        [DisplayName("GetCustomizeFormLayout reads the override of the session's tenant and defaults LayoutId to ProgId")]
        public void GetCustomizeFormLayout_SessionTenant_ReadsThatTenantsLayout()
        {
            var reader = new RecordingCustomizeReader();

            var result = CreateBo(CreateSession(TenantId), reader)
                .GetCustomizeFormLayout(new GetFormLayoutArgs { ProgId = "Employee" });

            Assert.Equal($"FormLayout:{TenantId}:Employee", Assert.Single(reader.Calls));
            var layout = XmlCodec.Deserialize<FormLayout>(result.Xml!)!;
            Assert.Equal("Employee", layout.LayoutId);
            Assert.Equal(TenantId, layout.Caption);
        }

        [Fact]
        [DisplayName("GetCustomizeFormLayout looks up the explicit LayoutId when one is given")]
        public void GetCustomizeFormLayout_ExplicitLayoutId_UsesIt()
        {
            var reader = new RecordingCustomizeReader();

            CreateBo(CreateSession(TenantId), reader)
                .GetCustomizeFormLayout(new GetFormLayoutArgs { ProgId = "Employee", LayoutId = "EmployeeCompact" });

            Assert.Equal($"FormLayout:{TenantId}:EmployeeCompact", Assert.Single(reader.Calls));
        }

        [Fact]
        [DisplayName("GetCustomizeFormLayout returns empty XML without asking the reader when the session has no tenant")]
        public void GetCustomizeFormLayout_NoTenant_ReturnsEmptyWithoutLookup()
        {
            var reader = new RecordingCustomizeReader();

            var result = CreateBo(CreateSession(string.Empty), reader)
                .GetCustomizeFormLayout(new GetFormLayoutArgs { ProgId = "Employee" });

            Assert.Equal(string.Empty, result.Xml);
            Assert.Empty(reader.Calls);
        }

        [Fact]
        [DisplayName("GetCustomizeFormLayout returns empty XML when the tenant has no override")]
        public void GetCustomizeFormLayout_NoOverride_ReturnsEmpty()
        {
            var reader = new RecordingCustomizeReader { HasOverride = false };

            var result = CreateBo(CreateSession(TenantId), reader)
                .GetCustomizeFormLayout(new GetFormLayoutArgs { ProgId = "Employee" });

            Assert.Equal(string.Empty, result.Xml);
            Assert.Single(reader.Calls);
        }

        [Fact]
        [DisplayName("GetCustomizeFormLayout rejects a blank ProgId with a UserMessageException")]
        public void GetCustomizeFormLayout_BlankProgId_Throws()
        {
            var bo = CreateBo(CreateSession(TenantId), new RecordingCustomizeReader());

            Assert.Throws<UserMessageException>(() => bo.GetCustomizeFormLayout(new GetFormLayoutArgs()));
        }

        [Fact]
        [DisplayName("GetCustomizeLanguage reads the override of the session's tenant")]
        public void GetCustomizeLanguage_SessionTenant_ReadsThatTenantsResource()
        {
            var reader = new RecordingCustomizeReader();

            var result = CreateBo(CreateSession(TenantId), reader)
                .GetCustomizeLanguage(new GetLanguageArgs { Lang = "zh-TW", Namespace = "Employee" });

            Assert.Equal($"Language:{TenantId}:zh-TW:Employee", Assert.Single(reader.Calls));
            Assert.Equal("Employee", XmlCodec.Deserialize<LanguageResource>(result.Xml!)!.Namespace);
        }

        [Fact]
        [DisplayName("GetCustomizeLanguage returns empty XML without asking the reader when the session has no tenant")]
        public void GetCustomizeLanguage_NoTenant_ReturnsEmptyWithoutLookup()
        {
            var reader = new RecordingCustomizeReader();

            var result = CreateBo(CreateSession(string.Empty), reader)
                .GetCustomizeLanguage(new GetLanguageArgs { Lang = "zh-TW", Namespace = "Employee" });

            Assert.Equal(string.Empty, result.Xml);
            Assert.Empty(reader.Calls);
        }
    }
}
