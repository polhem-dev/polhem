using System.ComponentModel;
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Definitions;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.ApiBridge.Definitions;

namespace Polhem.UI.ApiBridge.UnitTests
{
    /// <summary>
    /// <see cref="ApiFormDefinitionSource"/> over a connector that answers from memory: the stored path without a
    /// definition loader, and the localized path with one.
    /// </summary>
    public class ApiFormDefinitionSourceTests
    {
        private const string ProgId = "Employee";

        /// <summary>Answers each definition read from the objects it was given.</summary>
        private sealed class MemoryConnector(FormLayout? layout) : SystemApiConnector(
            PolhemApiClient.CreateLocal(EmptyServiceProvider.Instance))
        {
            public FormSchema Schema { get; } = CreateSchema();

            public CurrencySettings Currency { get; } = [];

            public UnitSettings Units { get; } = [];

            private static FormSchema CreateSchema()
            {
                var schema = new FormSchema(ProgId, "Employee");
                schema.Tables!.Add(ProgId, "Employee").Fields!.Add(new FormField("sys_name", "Name", FieldDbType.String));
                return schema;
            }

            public override Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null,
                CancellationToken cancellationToken = default)
            {
                object? value = defineType switch
                {
                    DefineType.FormSchema => Schema,
                    DefineType.FormLayout => layout,
                    DefineType.CurrencySettings => Currency,
                    DefineType.UnitSettings => Units,
                    DefineType.Language => new LanguageResource(),
                    _ => throw new NotSupportedException(defineType.ToString()),
                };
                return Task.FromResult((T)value!);
            }

            public override Task<FormLayout?> GetCustomizeFormLayoutAsync(string progId, string layoutId = "",
                CancellationToken cancellationToken = default)
                => Task.FromResult<FormLayout?>(null);

            public override Task<LanguageResource?> GetCustomizeLanguageAsync(string lang, string ns,
                CancellationToken cancellationToken = default)
                => Task.FromResult<LanguageResource?>(null);
        }

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public static readonly EmptyServiceProvider Instance = new();

            public object? GetService(Type serviceType) => null;
        }

        private static FormLayout StoredLayout() => new() { LayoutId = ProgId, ProgId = ProgId };

        private static ApiFormDefinitionSource WithoutLoader(MemoryConnector connector)
        {
            var access = new ClientDefineAccess(connector);
            return new ApiFormDefinitionSource(() => access, () => null);
        }

        private static ApiFormDefinitionSource WithLoader(MemoryConnector connector)
        {
            var access = new ClientDefineAccess(connector);
            var loader = new FormDefinitionLoader(access);
            return new ApiFormDefinitionSource(() => access, () => loader);
        }

        [Fact]
        [DisplayName("The constructor rejects a missing definition accessor")]
        public void Constructor_NullDefineAccess_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ApiFormDefinitionSource(null!, () => null));
        }

        [Fact]
        [DisplayName("The constructor rejects a missing loader accessor")]
        public void Constructor_NullLoader_Throws()
        {
            var access = new ClientDefineAccess(new MemoryConnector(null));

            Assert.Throws<ArgumentNullException>(() => new ApiFormDefinitionSource(() => access, null!));
        }

        [Fact]
        [DisplayName("Without a definition loader the schema is the stored one")]
        public async Task GetFormSchemaAsync_NoLoader_ReturnsStoredSchema()
        {
            var connector = new MemoryConnector(null);

            var schema = await WithoutLoader(connector).GetFormSchemaAsync(ProgId);

            Assert.Same(connector.Schema, schema);
        }

        [Fact]
        [DisplayName("With a definition loader the schema is a localized copy, not the stored instance")]
        public async Task GetFormSchemaAsync_WithLoader_ReturnsCopy()
        {
            var connector = new MemoryConnector(null);

            var schema = await WithLoader(connector).GetFormSchemaAsync(ProgId);

            Assert.NotSame(connector.Schema, schema);
            Assert.Equal(ProgId, schema.ProgId);
        }

        [Fact]
        [DisplayName("Without a definition loader the layout is a copy of the stored one")]
        public async Task GetFormLayoutAsync_NoLoader_ReturnsClone()
        {
            var stored = StoredLayout();
            var connector = new MemoryConnector(stored);

            var layout = await WithoutLoader(connector).GetFormLayoutAsync(ProgId, connector.Schema);

            Assert.NotSame(stored, layout);
            Assert.Equal(ProgId, layout.LayoutId);
        }

        [Fact]
        [DisplayName("Without a definition loader a missing layout names the file to author")]
        public async Task GetFormLayoutAsync_NoLoaderNoLayout_Throws()
        {
            var connector = new MemoryConnector(null);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => WithoutLoader(connector).GetFormLayoutAsync(ProgId, connector.Schema));

            Assert.Contains("FormLayout/Employee.FormLayout.xml", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("With a definition loader the layout is the runtime layout built from the stored one")]
        public async Task GetFormLayoutAsync_WithLoader_ReturnsRuntimeLayout()
        {
            var stored = StoredLayout();
            var connector = new MemoryConnector(stored);

            var layout = await WithLoader(connector).GetFormLayoutAsync(ProgId, connector.Schema);

            Assert.NotSame(stored, layout);
            Assert.Equal(ProgId, layout.LayoutId);
        }

        [Fact]
        [DisplayName("The currency settings come from the definition accessor")]
        public async Task GetCurrencySettingsAsync_Stored_ReturnsSettings()
        {
            var connector = new MemoryConnector(null);

            var settings = await WithoutLoader(connector).GetCurrencySettingsAsync();

            Assert.Same(connector.Currency, settings);
        }

        [Fact]
        [DisplayName("The unit settings come from the definition accessor")]
        public async Task GetUnitSettingsAsync_Stored_ReturnsSettings()
        {
            var connector = new MemoryConnector(null);

            var settings = await WithoutLoader(connector).GetUnitSettingsAsync();

            Assert.Same(connector.Units, settings);
        }
    }
}
