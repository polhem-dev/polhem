using System.ComponentModel;
using System.Reflection;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Repository;
using Polhem.Repository.Abstractions;
using Polhem.Repository.Abstractions.Factories;
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

        // "Type.Method" entries allowed to read the session's company members, with why.
        private static readonly HashSet<string> s_allowedReaders = new(StringComparer.Ordinal)
        {
            // The snapshots themselves: the request's, taken by the factory, and a business object's own when it
            // was constructed without one.
            "BusinessObjectFactory.ReadCompanyScope",
            "BusinessObject.get_CompanyScope",
            // Public access-token entry points, each reading the session once and handing the read to the
            // snapshot overload. Framework code inside a request does not call them (the second test).
            "CompanyAuthorizationService.Can",
            "ScopeResolver.ResolveFilter",
            "RepositoryDatabaseRouter.Resolve",
            "ProgramSettingsRepositoryTypeResolver.GetCustomizeId",
            // A repository built without the request's snapshot, by code outside a business object, reads the
            // session once and takes both its database and its company from that read.
            "RepositoryBase.ReadSessionCompanyScope",
            // The write side: entering, leaving and signing out change the scope, and persist it.
            "SystemBusinessObject.LeaveCompany",
            "SystemBusinessObject.Logout",
            "SystemBusinessObject.CreateSeed",
        };

        // "Type.Method" entries allowed to call an access-token entry point that reads the session itself.
        private static readonly HashSet<string> s_allowedTokenCallers = new(StringComparer.Ordinal)
        {
            // The access-token overloads of the extensions are public API for host code without a snapshot.
            "BusinessObjectFactoryExtensions.CreateFormBO",
            "BusinessObjectFactoryExtensions.CreateSystemBO",
            // Routes a repository that is not company-bound (Common, Log), which reads no session.
            "RepositoryBase..ctor",
        };

        private static bool IsCompanyRead(MemberInfo member)
            => member.DeclaringType == typeof(SessionInfo) && s_companyMembers.Contains(member.Name, StringComparer.Ordinal);

        private static bool IsTokenEntryPoint(MemberInfo member)
        {
            if (member is not MethodInfo method) { return false; }
            var parameters = method.GetParameters();
            // An overload that also takes the snapshot uses it; only one without it reads the session itself.
            var takesToken = parameters.Any(p => p.ParameterType == typeof(Guid))
                && parameters.All(p => p.ParameterType != typeof(SessionCompanyScope));
            var type = method.DeclaringType;
            return takesToken && (
                (type == typeof(ICompanyAuthorizationService) && method.Name == nameof(ICompanyAuthorizationService.Can))
                || (type == typeof(IScopeResolver) && method.Name == nameof(IScopeResolver.ResolveFilter))
                || (type == typeof(IRepositoryDatabaseRouter) && method.Name == nameof(IRepositoryDatabaseRouter.Resolve))
                || (type == typeof(IRepositoryFactory) && method.Name == nameof(IRepositoryFactory.CreateFormRepository))
                || (type == typeof(IBusinessObjectFactory) && method.Name == nameof(IBusinessObjectFactory.CreateBusinessObject)));
        }

        private static IEnumerable<(string Where, MemberInfo Member)> Calls()
            => s_assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsNested && t.Namespace?.StartsWith("Polhem.", StringComparison.Ordinal) == true)
                .SelectMany(IlMemberReferences.MethodsOf)
                .SelectMany(m => IlMemberReferences.Of(m).Select(r => ($"{IlMemberReferences.OuterType(m)!.Name}.{SourceName(m)}", r)));

        // A compiler-generated method is named after the method it was written in: `<LoadAsync>d__5` or
        // `<GetList>b__3_0`. The state machine's own `MoveNext` takes its name from the nested type.
        private static string SourceName(MethodBase method)
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

            Assert.Contains(calls, c => c.Where == "BusinessObject.get_CompanyScope" && IsCompanyRead(c.Member));
            Assert.Contains(calls, c => c.Where.StartsWith("FormBusinessObject.", StringComparison.Ordinal)
                && c.Member is MethodInfo { Name: nameof(ICompanyAuthorizationService.Can) } can
                && can.GetParameters()[0].ParameterType == typeof(SessionCompanyScope));
            Assert.Contains(calls, c => c.Where == "BusinessObjectFactoryExtensions.CreateFormBO" && IsTokenEntryPoint(c.Member));
            Assert.Contains(calls, c => c.Where == "CompanyAuthorizationService.Can" && IsCompanyRead(c.Member));
        }
    }
}
