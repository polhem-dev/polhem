using System.ComponentModel;
using System.Reflection;
using Polhem.Definition;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Contracts
{
    /// <summary>
    /// Guards that every reserved progId really can be constructed by <see cref="BusinessObjectFactory"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the existing gates cannot catch this.</b> <c>ActionSurfaceTests</c> guards the symmetry of action constants and
    /// methods, <c>BoApiSurfaceTests</c> guards the public surface, and <c>ReservedProgIdResolutionTests</c> guards which
    /// <b>type</b> a progId resolves to. All of them only mean something after the type has been constructed. Yet
    /// <c>BusinessObjectFactory.CreateBusinessObject</c> constructs it with <c>Activator.CreateInstance</c> and a fixed argument list,
    /// and <b>C# constructors are not inherited</b>: a subclass that declares one parameter too few leaves those gates green,
    /// and the symptom is a runtime <c>MissingMethodException</c> that happens <b>before</b> the method lookup
    /// and reaches the caller as <c>InternalError</c>.
    /// </para>
    /// <para>
    /// This is exactly what happened to <c>AuditRule</c> in 4.25.0: it declared only a three-parameter constructor, so the audit rule
    /// maintenance form shipped with the framework was completely unreachable remotely, and the only test constructed it directly with <c>new</c>, never through the factory,
    /// so the whole suite was green when it shipped.
    /// </para>
    /// <para>
    /// <b>Two complementary layers, overlapping on purpose.</b> <c>CreateBusinessObject_*</c> goes through the real factory and is the layer closest to the actual failure, while
    /// <c>DefaultType_DeclaresConstructorMatchingTheBase</c> is pure reflection and does not even go through the factory. Neither
    /// <b>hard-codes the argument shape the factory passes</b>: the first does not need to know it, and the second derives it from the constructor of the
    /// <see cref="BusinessObject"/> base. Copying the shape would add another source that drifts.
    /// </para>
    /// <para>
    /// NOTE: the factory layer used to need the database container. It used a bare <c>Guid.NewGuid()</c> as the token, and during BO construction
    /// <c>SessionInfoService.Get</c> found nothing and took the rebuild path that reads <c>st_session</c>. That dependency has nothing to do with this test's
    /// subject (the constructor shape), so it was removed by switching to <see cref="TestSessionFactory.CreateAccessToken"/>,
    /// and the fixture was downgraded from <c>SharedDbFixture</c> to <see cref="PolhemTestFixture"/>.
    /// </para>
    /// </remarks>
    public class ReservedProgIdConstructionTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public ReservedProgIdConstructionTests(PolhemTestFixture fx) { _fx = fx; }

        private IBusinessObjectFactory Factory => _fx.GetRequiredService<IBusinessObjectFactory>();

        public static TheoryData<string> ReservedProgIds()
        {
            var data = new TheoryData<string>();
            foreach (var binding in Polhem.Business.ReservedProgIds.All)
                data.Add(binding.ProgId);
            return data;
        }

        [Theory]
        [MemberData(nameof(ReservedProgIds))]
        [DisplayName("Every reserved progId can be constructed into its BO through BusinessObjectFactory")]
        public void CreateBusinessObject_EveryReservedProgId_Succeeds(string progId)
        {
            var binding = Polhem.Business.ReservedProgIds.Find(progId);
            Assert.NotNull(binding);

            var bo = Factory.CreateBusinessObject(TestSessionFactory.CreateAccessToken(_fx), progId, isLocalCall: true);

            // The assertion uses `ExpectedBaseType` rather than `DefaultType`: a deployment may bind a reserved progId to its own subclass in the registry,
            // which is legitimate, and whichever type it binds must satisfy that progId's base constraint.
            Assert.IsType(binding!.ExpectedBaseType, bo, exactMatch: false);
        }

        [Theory]
        [MemberData(nameof(ReservedProgIds))]
        [DisplayName("The BO constructed for every reserved progId keeps isLocalCall=false")]
        public void CreateBusinessObject_EveryReservedProgId_PreservesRemoteFlag(string progId)
        {
            var bo = Factory.CreateBusinessObject(TestSessionFactory.CreateAccessToken(_fx), progId, isLocalCall: false);

            var businessObject = Assert.IsType<BusinessObject>(bo, exactMatch: false);
            Assert.False(businessObject.IsLocalCall);
        }

        [Theory]
        [MemberData(nameof(ReservedProgIds))]
        [DisplayName("The default BO of every reserved progId declares a constructor with the same parameter shape as the base (no container needed)")]
        public void DefaultType_DeclaresConstructorMatchingTheBase(string progId)
        {
            var binding = Polhem.Business.ReservedProgIds.Find(progId);
            Assert.NotNull(binding);

            // The expected shape is derived from the `BusinessObject` base itself, not copied from the factory, because a copy would be another source that drifts.
            var expected = typeof(BusinessObject)
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(c => c.GetParameters().Select(p => p.ParameterType).ToArray())
                .OrderByDescending(types => types.Length)
                .First();

            var actual = binding!.DefaultType.GetConstructor(expected);

            Assert.True(
                actual is not null,
                $"{binding.DefaultType.Name} has no ({string.Join(", ", expected.Select(t => t.Name))}) constructor, " +
                "so BusinessObjectFactory throws MissingMethodException and the progId is unreachable remotely.");
        }

        [Fact]
        [DisplayName("The reserved progId list is not empty (guards against a vacuous pass: with an empty list the theories above would always be green)")]
        public void ReservedProgIds_AreNotEmpty()
        {
            Assert.NotEmpty(Polhem.Business.ReservedProgIds.All);
        }
    }
}
