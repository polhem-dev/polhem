using System.ComponentModel;
using System.Reflection;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Repository;
using Polhem.Repository.Abstractions;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Factories;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Framework code reads the session's company scope only where a request takes its one snapshot, and passes the
    /// snapshot on rather than an access token for the next layer to read again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A concurrent <c>EnterCompany</c> on the same token replaces the session's company scope at any moment. Two
    /// reads in one request can see two companies, which once let a request be authorized with one company's roles
    /// and then reach another company's data (ADR-063). Each listed exception below is a place that takes the
    /// snapshot, writes the scope, or keeps a public access-token entry point that reads it exactly once.
    /// </para>
    /// <para>
    /// The scan reads compiled IL, closures and async state machines included, and attributes a call to the source
    /// type it was written in.
    /// </para>
    /// </remarks>
    public class SessionCompanyScopeReadGateTests
    {
        private static readonly string[] s_companyMembers =
        [
            "get_" + nameof(SessionInfo.CompanyScope),
            "get_" + nameof(SessionInfo.CompanyId),
            "get_" + nameof(SessionInfo.CustomizeId),
            "get_" + nameof(SessionInfo.Roles),
            "get_" + nameof(SessionInfo.UserRowId),
            "get_" + nameof(SessionInfo.EmployeeRowId),
            "get_" + nameof(SessionInfo.DeptRowId),
        ];

        private static readonly Assembly[] s_assemblies =
        [
            typeof(BusinessObject).Assembly,
            typeof(RepositoryBase).Assembly,
            typeof(Polhem.ObjectCaching.Services.CompanyAuthorizationService).Assembly,
        ];

        // "Type.Method(parameter types)" entries allowed to read the session's company members, with why. The
        // parameter types keep an allowance from covering an overload of the same name.
        private static readonly HashSet<string> s_allowedReaders = new(StringComparer.Ordinal)
        {
            // The snapshots themselves: the request's, taken by the factory, and a business object's or a plugin's
            // own when it was constructed without one.
            "BusinessObjectFactory.ReadCompanyScope(Guid)",
            "BusinessObject.ReadCompanyScope()",
            "FormBusinessPlugin.ReadCompanyScope()",
            // Public access-token entry points, each reading the session once and handing the read to the
            // snapshot overload. Framework code inside a request does not call them (the second test).
            "CompanyAuthorizationService.Can(Guid, String, PermissionActions)",
            "ScopeResolver.ResolveFilter(Guid, String, PermissionActions, FormSchema)",
            "RepositoryDatabaseRouter.Resolve(DbScope, Guid)",
            "ProgramSettingsRepositoryTypeResolver.GetCustomizeId(Guid)",
            // A repository built without the request's snapshot, by code outside a business object, reads the
            // session once and takes both its database and its company from that read.
            "RepositoryBase.ReadSessionCompanyScope(IRepositoryContext, Guid)",
            // The write side: leaving and signing out change the scope, and the seed persists it.
            "SystemBusinessObject.LeaveCompany(LeaveCompanyArgs)",
            "SystemBusinessObject.Logout(LogoutArgs)",
            "SystemBusinessObject.CreateSeed(SessionInfo)",
        };

        // "Type.Method(parameter types)" entries allowed to call an access-token entry point.
        private static readonly HashSet<string> s_allowedTokenCallers = new(StringComparer.Ordinal)
        {
            // The access-token overloads of the extensions are public API for host code without a snapshot.
            "BusinessObjectFactoryExtensions.CreateFormBO(IBusinessObjectFactory, Guid, String, Boolean)",
            "BusinessObjectFactoryExtensions.CreateSystemBO(IBusinessObjectFactory, Guid, Boolean)",
            // The factory's own access-token path, for code without a snapshot.
            "RepositoryFactory.CreateFormRepository(Guid, String)",
            "RepositoryFactory.CreateFormRepositoryCore(Guid, String)",
        };

        // Services whose access-token members read the session themselves.
        private static readonly Type[] s_sessionReadingServices =
        [
            typeof(ICompanyAuthorizationService),
            typeof(IScopeResolver),
            typeof(IRepositoryDatabaseRouter),
            typeof(IRepositoryFactory),
            typeof(IRepositoryTypeResolver),
            typeof(IBusinessObjectFactory),
        ];

        private static readonly string[] s_entryPointNames =
        [
            nameof(ICompanyAuthorizationService.Can),
            nameof(IScopeResolver.ResolveFilter),
            nameof(IRepositoryDatabaseRouter.Resolve),
            nameof(IRepositoryFactory.CreateFormRepository),
            "CreateFormRepositoryCore",
            nameof(IBusinessObjectFactory.CreateBusinessObject),
        ];

        private static bool IsCompanyRead(MemberInfo member)
            => member.DeclaringType == typeof(SessionInfo) && s_companyMembers.Contains(member.Name, StringComparer.Ordinal);

        // Matches the member on the interface and on any implementation, so a call through a concrete type counts.
        private static bool IsTokenEntryPoint(MemberInfo member)
        {
            if (member is not MethodInfo method || method.DeclaringType is not { } type) { return false; }
            if (!s_entryPointNames.Contains(method.Name, StringComparer.Ordinal)) { return false; }
            if (!s_sessionReadingServices.Any(service => service.IsAssignableFrom(type))) { return false; }
            var parameters = method.GetParameters();
            // An overload that also takes the snapshot uses it; only one without it reads the session itself.
            return parameters.Any(p => p.ParameterType == typeof(Guid))
                && parameters.All(p => p.ParameterType != typeof(SessionCompanyScope));
        }

        private static IEnumerable<(string Where, MemberInfo Member)> Calls()
            => s_assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsNested && t.Namespace?.StartsWith("Polhem.", StringComparison.Ordinal) == true)
                .SelectMany(IlMemberReferences.MethodsOf)
                .SelectMany(m => IlMemberReferences.Of(m).Select(r => ($"{IlMemberReferences.OuterType(m)!.Name}.{SourceName(m)}", r)));

        // A method written in source is named with its parameter types; a compiler-generated one (a closure, a state
        // machine) cannot be, and goes by the name of the method it was written in.
        private static string SourceName(MethodBase method)
        {
            var name = SourceMethodName(method);
            if (method.DeclaringType?.DeclaringType != null && method.DeclaringType.Name.StartsWith('<')) { return name; }
            if (method.Name.StartsWith('<')) { return name; }
            return $"{name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})";
        }

        // A compiler-generated method is named after the method it was written in: `<LoadAsync>d__5` or
        // `<GetList>b__3_0`. The state machine's own `MoveNext` takes its name from the nested type.
        private static string SourceMethodName(MethodBase method)
        {
            var name = method.Name == "MoveNext" && method.DeclaringType?.Name.StartsWith('<') == true
                ? method.DeclaringType.Name
                : method.Name;
            if (!name.StartsWith('<')) { return name; }
            var end = name.IndexOf('>', StringComparison.Ordinal);
            return end > 1 ? name[1..end] : name;
        }

        [Fact]
        [DisplayName("Framework code reads the session's company scope only where a request takes its snapshot")]
        public void CompanyScope_IsReadOnlyAtSnapshotPoints()
        {
            var offenders = Calls()
                .Where(c => IsCompanyRead(c.Member) && !s_allowedReaders.Contains(c.Where))
                .Select(c => $"{c.Where} reads SessionInfo.{c.Member.Name[4..]}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.True(offenders.Length == 0,
                "Read the request's snapshot (BusinessObject.CompanyScope) instead:" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        [DisplayName("Framework code passes the request's snapshot on, not an access token for the next layer to read again")]
        public void TokenEntryPoints_AreNotCalledInsideARequest()
        {
            var offenders = Calls()
                .Where(c => IsTokenEntryPoint(c.Member) && !s_allowedTokenCallers.Contains(c.Where))
                .Select(c => $"{c.Where} calls {c.Member.DeclaringType!.Name}.{c.Member.Name}(…Guid…)")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.True(offenders.Length == 0,
                "Call the overload that takes a SessionCompanyScope:" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        [DisplayName("The scan sees the snapshot's own reads and calls, so the gate cannot pass vacuously")]
        public void Scan_SeesKnownReadsAndCalls()
        {
            var calls = Calls().ToArray();

            Assert.Contains(calls, c => c.Where == "BusinessObject.ReadCompanyScope()" && IsCompanyRead(c.Member));
            Assert.Contains(calls, c => c.Where.StartsWith("FormBusinessObject.", StringComparison.Ordinal)
                && c.Member is MethodInfo { Name: nameof(ICompanyAuthorizationService.Can) } can
                && can.GetParameters()[0].ParameterType == typeof(SessionCompanyScope));
            Assert.Contains(calls, c => c.Where == "BusinessObjectFactoryExtensions.CreateFormBO(IBusinessObjectFactory, Guid, String, Boolean)"
                && IsTokenEntryPoint(c.Member));
            // A call through a concrete type counts: the factory's token path calls its own resolver by interface,
            // and its own core by the concrete type.
            Assert.Contains(calls, c => c.Where == "RepositoryFactory.CreateFormRepository(Guid, String)" && IsTokenEntryPoint(c.Member));
            Assert.Contains(calls, c => c.Where == "CompanyAuthorizationService.Can(Guid, String, PermissionActions)" && IsCompanyRead(c.Member));
        }
    }
}
