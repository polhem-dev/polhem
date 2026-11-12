using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Definitions;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;

namespace Polhem.Api.Client.UnitTests.Definitions
{
    /// <summary>
    /// How <see cref="FormDefinitionLoader"/> localizes the option sets a schema borrows from a shared namespace.
    /// </summary>
    /// <remarks>
    /// The server resolves a namespace without regard to case, so the client must too: a schema whose fields write
    /// the same namespace in two cases fetches it once, and both fields read from that one copy.
    /// </remarks>
    public class FormDefinitionLoaderLanguageTests
    {
        private const string ProgId = "Employee";
        private const string Lang = "zh-TW";

        private sealed class LanguageConnector : SystemApiConnector
        {
            public LanguageConnector() : base(TestClients.Local(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid())) { }

            public override Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null, CancellationToken cancellationToken = default)
            {
                if (defineType == DefineType.FormSchema)
                {
                    var schema = new FormSchema(ProgId, "Employee");
                    var table = schema.Tables!.Add(ProgId, "Employee");
                    table.Fields!.Add(new FormField("gender", "Gender", FieldDbType.String) { LangEnumName = "common.Gender" });
                    table.Fields!.Add(new FormField("status", "Status", FieldDbType.String) { LangEnumName = "Common.Status" });
                    return Task.FromResult((T)(object)schema);
                }

                if (defineType == DefineType.Language && string.Equals(keys![1], "Common", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult((T)(object)CommonResource());

                throw new FileNotFoundException("No language resource in this fake.");
            }

            public override Task<LanguageResource?> GetCustomizeLanguageAsync(string lang, string ns, CancellationToken cancellationToken = default)
                => Task.FromResult<LanguageResource?>(null);
        }

        private static LanguageResource CommonResource()
        {
            var resource = new LanguageResource { Namespace = "Common", Lang = Lang };
            var gender = new LanguageEnum { Name = "Gender" };
            gender.Entries.Add("M", "男");
            resource.Enums.Add(gender);
            var status = new LanguageEnum { Name = "Status" };
            status.Entries.Add("A", "在職");
            resource.Enums.Add(status);
            return resource;
        }

        [Fact]
        [DisplayName("GetLocalizedSchemaAsync translates enums whose shared namespace is written in different cases")]
        public async Task GetLocalizedSchemaAsync_NamespaceInTwoCases_TranslatesBothEnums()
        {
            var loader = new FormDefinitionLoader(new ClientDefineAccess(new LanguageConnector()));

            var schema = await loader.GetLocalizedSchemaAsync(ProgId, Lang);

            var fields = schema.Tables![ProgId].Fields!;
            Assert.Equal("男", Assert.Single(fields["gender"].ListItems!).Text);
            Assert.Equal("在職", Assert.Single(fields["status"].ListItems!).Text);
        }
    }
}
