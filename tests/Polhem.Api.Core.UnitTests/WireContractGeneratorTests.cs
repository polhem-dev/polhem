using System.ComponentModel;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Pins the generated TypeScript contract: change a message type and this goes red.
    /// </summary>
    /// <remarks>
    /// The same pattern as <c>WireFixtureTests</c>, guarding the other half: the fixtures pin what the values look
    /// like, and this pins which fields the types have. Neither exists to prevent changes; both keep changes from
    /// happening unnoticed. Renaming a field is a breaking change for older cross-language clients, and the compiler
    /// cannot see that side.
    /// <para>
    /// To regenerate (**only when deliberately changing the contract**):
    /// <c>POLHEM_REGENERATE_WIRE_CONTRACTS=1 dotnet test tests/Polhem.Api.Core.UnitTests/…</c>
    /// then read through the diff before committing.
    /// </para>
    /// </remarks>
    public class WireContractGeneratorTests
    {
        private static string TypeNamesPath() =>
            Path.Combine(Path.GetDirectoryName(ContractPath())!, "type-names.ts");

        private static string ContractPath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Polhem.slnx")))
                dir = dir.Parent;

            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "wire-contracts", "messages.d.ts");
        }

        private static bool RegenerateRequested =>
            Environment.GetEnvironmentVariable("POLHEM_REGENERATE_WIRE_CONTRACTS") == "1";

        [Fact]
        [DisplayName("The generated TypeScript contract matches the current message types")]
        public void GeneratedContract_MatchesMessageTypes()
        {
            var generated = WireContractGenerator.Generate();
            var path = ContractPath();

            if (RegenerateRequested)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, generated);
                return;
            }

            Assert.True(File.Exists(path), $"The contract file does not exist ({path}). Generate it with POLHEM_REGENERATE_WIRE_CONTRACTS=1.");

            var actual = File.ReadAllText(path);
            Assert.True(string.Equals(actual, generated, StringComparison.Ordinal),
                "The message types do not match the generated TypeScript contract." + Environment.NewLine +
                "If this is a deliberate contract change, regenerate with POLHEM_REGENERATE_WIRE_CONTRACTS=1 and read every line of the diff. " +
                "Renaming or removing a field is a breaking change for clients that are not released with the framework.");
        }

        [Fact]
        [DisplayName("The generated type name map matches the current message types")]
        public void GeneratedTypeNames_MatchMessageTypes()
        {
            var generated = WireContractGenerator.GenerateTypeNames();
            var path = TypeNamesPath();

            if (RegenerateRequested)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, generated);
                return;
            }

            Assert.True(File.Exists(path), $"The type name map does not exist ({path}). Generate it with POLHEM_REGENERATE_WIRE_CONTRACTS=1.");

            Assert.True(string.Equals(File.ReadAllText(path), generated, StringComparison.Ordinal),
                "The message types do not match the generated type name map." + Environment.NewLine +
                "Moving a message type to another namespace or renaming it makes cross-language clients send a type name the server cannot resolve. " +
                "The symptom is a rejection at run time, not a compile error.");
        }

        [Fact]
        [DisplayName("Generated type names are assembly-qualified, otherwise the server cannot resolve them")]
        public void GeneratedTypeNames_AreAssemblyQualified()
        {
            var generated = WireContractGenerator.GenerateTypeNames();

            Assert.Contains(
                "LoginRequest: 'Polhem.Api.Core.Messages.System.LoginRequest, Polhem.Api.Core',",
                generated, StringComparison.Ordinal);
            Assert.Contains(
                "GetListRequest: 'Polhem.Api.Core.Messages.Form.GetListRequest, Polhem.Api.Core',",
                generated, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The generated contract is not vacuous: key message types and wire-specific shapes are present")]
        public void GeneratedContract_IsNotVacuous()
        {
            var generated = WireContractGenerator.Generate();

            // Named canaries rather than a minimum count: a namespace move can empty the output while a count check still passes.
            string[] required =
            [
                "export interface LoginRequest {",
                "export interface GetListRequest {",
                "export interface PingResponse {",
                // Wire-specific shapes: reflection cannot see these, because custom converters decide them.
                "export type WireValueEnvelope =",
                "export interface DataSet {",
                "export interface DataTable {",
            ];
            foreach (var fragment in required)
                Assert.Contains(fragment, generated, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The contract describes the wire shape, not the CLR shape: Guid and DateTime are strings and enums are string literal unions")]
        public void GeneratedContract_DescribesWireShapeNotClrShape()
        {
            var generated = WireContractGenerator.Generate();

            // `LoginResponse.AccessToken` is a Guid, which is a string in JSON. It is optional because the JSON wires
            // leave out an empty Guid like any other default value.
            Assert.Contains("accessToken?: string;", generated, StringComparison.Ordinal);
            // Enums go on the wire as strings (`JsonStringEnumConverter`), not numbers.
            Assert.Contains("export type DefineType = '", generated, StringComparison.Ordinal);
            Assert.DoesNotContain(": Guid;", generated, StringComparison.Ordinal);
            Assert.DoesNotContain(": DateTime;", generated, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A polymorphic member is the union of its concrete shapes, each carrying its discriminator")]
        public void GeneratedContract_RendersPolymorphicFilterAsUnion()
        {
            var generated = WireContractGenerator.Generate();

            Assert.Contains("export type FilterNode = FilterCondition | FilterGroup;", generated, StringComparison.Ordinal);
            Assert.Contains("export interface FilterCondition {", generated, StringComparison.Ordinal);
            Assert.Contains("export interface FilterGroup {", generated, StringComparison.Ordinal);
            // `Condition` is the enum's default, so the JSON wires leave it out and the reader assumes it when absent.
            Assert.Contains("  kind?: 'Condition';", generated, StringComparison.Ordinal);
            Assert.Contains("  kind: 'Group';", generated, StringComparison.Ordinal);
            Assert.Contains("export type ComparisonOperator = '", generated, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A value-typed member is optional unless the server always writes it, because the JSON wires omit default values")]
        public void GeneratedContract_MarksDefaultOmittedValueMembersOptional()
        {
            var generated = WireContractGenerator.Generate();

            // `PagingInfo.HasMore` is left out when false.
            Assert.Contains("  hasMore?: boolean;", generated, StringComparison.Ordinal);
            // `CreateSessionRequest.ExpiresIn` initialises to 3600, so it is always written and therefore required.
            Assert.Contains("  expiresIn: number;", generated, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A dictionary member is a string-keyed record of its value type, not an array of its key type")]
        public void GeneratedContract_RendersDictionaryAsRecord()
        {
            var generated = WireContractGenerator.Generate();

            Assert.Contains("  affectedRows?: Record<string, number>;", generated, StringComparison.Ordinal);
            Assert.Contains("  capabilities?: Record<string, PermissionAction>;", generated, StringComparison.Ordinal);
            Assert.DoesNotContain("affectedRows?: string[];", generated, StringComparison.Ordinal);
        }
    }
}