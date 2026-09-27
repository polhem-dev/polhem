using System.ComponentModel;
using System.Reflection;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Security;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// Gate: every constant of <see cref="BackendDefaultTypes"/> must resolve to a type that satisfies its contract.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These constants are assembly-qualified **strings** pointing to concrete types in <c>Polhem.Business</c>,
    /// <c>Polhem.ObjectCaching</c> and <c>Polhem.Repository</c>. The compiler cannot see these edges and the
    /// dependency graph does not show them: rename or move <c>MemoryCacheProvider</c> and nothing fails here; the
    /// host only blows up at startup.
    /// </para>
    /// <para>
    /// The constants live in <c>Polhem.Hosting</c>, which references every one of those assemblies, but they stay
    /// strings so that a default is loaded through the same path as a name a deployment configures. That keeps
    /// the edges invisible to the compiler, and this gate turns "strings that rot silently" into "a contract that
    /// goes red when the tests run".
    /// </para>
    /// </remarks>
    public class BackendDefaultTypesGateTests
    {
        /// <summary>
        /// The contract each constant must satisfy. **A new constant must be registered here**, otherwise
        /// <see cref="EveryConstant_HasADeclaredContract"/> goes red.
        /// </summary>
        private static readonly Dictionary<string, Type> s_expectedContracts = new(StringComparer.Ordinal)
        {
            [nameof(BackendDefaultTypes.ApiEncryptionKeyProvider)] = typeof(IApiEncryptionKeyProvider),
            [nameof(BackendDefaultTypes.AccessTokenValidator)] = typeof(IAccessTokenValidator),
            [nameof(BackendDefaultTypes.CacheDataSourceProvider)] = typeof(ICacheDataSourceProvider),
            [nameof(BackendDefaultTypes.DefineStorage)] = typeof(IDefineStorage),
            [nameof(BackendDefaultTypes.DefineAccess)] = typeof(IDefineAccess),
            [nameof(BackendDefaultTypes.SessionInfoService)] = typeof(ISessionInfoService),
            [nameof(BackendDefaultTypes.CompanyInfoService)] = typeof(ICompanyInfoService),
            [nameof(BackendDefaultTypes.RepositoryFactory)] = typeof(IRepositoryFactory),
        };

        public static TheoryData<string, string> Constants
        {
            get
            {
                var data = new TheoryData<string, string>();
                foreach (var (name, value) in ReadConstants())
                    data.Add(name, value);
                return data;
            }
        }

        [Theory]
        [MemberData(nameof(Constants))]
        [DisplayName("Every default type constant resolves to a type assignable to its contract")]
        public void Constant_ResolvesToATypeSatisfyingItsContract(string name, string typeName)
        {
            var contract = s_expectedContracts[name];

            // `Type.GetType` only finds loaded assemblies, so touch one type in each target assembly first
            // to load them.
            ForceLoadBackendAssemblies();
            var type = Type.GetType(typeName, throwOnError: false);

            Assert.True(type != null,
                $"BackendDefaultTypes.{name} points to '{typeName}', but no type could be resolved. " +
                "This constant is the host's startup default, so a rotted string only shows up at run time.");
            Assert.True(contract.IsAssignableFrom(type),
                $"BackendDefaultTypes.{name} points to {type!.FullName}, but it does not implement {contract.Name}.");
        }

        [Fact]
        [DisplayName("Every constant is registered in the contract table (a new constant cannot bypass this gate)")]
        public void EveryConstant_HasADeclaredContract()
        {
            var declared = ReadConstants().Select(c => c.Name).ToList();

            var unregistered = declared.Where(n => !s_expectedContracts.ContainsKey(n)).ToList();
            var stale = s_expectedContracts.Keys.Where(n => !declared.Contains(n, StringComparer.Ordinal)).ToList();

            Assert.True(unregistered.Count == 0,
                $"These constants are not registered in s_expectedContracts, so this gate does not protect them: {string.Join(", ", unregistered)}");
            Assert.True(stale.Count == 0,
                $"These registered entries no longer have a matching constant: {string.Join(", ", stale)}");
        }

        [Fact]
        [DisplayName("The gate really finds the constants (guards against a vacuously true empty loop)")]
        public void Gate_IsNotVacuous()
        {
            // If the constants change to another member form (such as `static readonly`), the reflection filter
            // matches nothing and the two tests above become empty loops.
            Assert.True(ReadConstants().Count >= 8,
                $"Only {ReadConstants().Count} constants were found; the reflection filter may no longer match the actual shape of the type.");
        }

        private static List<(string Name, string Value)> ReadConstants() =>
            typeof(BackendDefaultTypes)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .Select(f => (f.Name, (string)f.GetRawConstantValue()!))
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// Touches one type in each target assembly to make sure they are loaded. Otherwise
        /// <see cref="Type.GetType(string, bool)"/> returns <c>null</c> for an assembly that is not loaded yet and the
        /// gate reports a false failure.
        /// </summary>
        private static void ForceLoadBackendAssemblies()
        {
            _ = typeof(Polhem.Business.BusinessObjectFactory);
            _ = typeof(Polhem.ObjectCaching.CacheDefineAccess);
            _ = typeof(Polhem.Repository.Factories.RepositoryFactory);
        }
    }
}
