using System.ComponentModel;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// <see cref="FormPluginChain"/> and <see cref="FormPluginRunner"/>: the declared stage must match the class's override
    /// exactly, plugins run in declaration order, instances are constructed on demand, and each operation gets its own instances.
    /// </summary>
    public class FormPluginRunnerTests : IClassFixture<PolhemTestFixture>
    {
        private readonly IBusinessObjectContext _ctx;

        public FormPluginRunnerTests(PolhemTestFixture fixture)
        {
            _ctx = TestBusinessObjectContext.Create(fixture);
        }

        private static FormPluginChain Chain(params FormPluginBinding[] bindings)
            => FormPluginChain.Create("Order", bindings);

        private FormPluginRunner CreateRunner(params FormPluginBinding[] bindings)
            => Chain(bindings).CreateRunner(_ctx, Guid.NewGuid(), "Order");

        private static FormPluginBinding Bind<T>(PluginStage stage) where T : FormBusinessPlugin
            => new(typeof(T), stage);

        [Fact]
        [DisplayName("The chain records each type's stage as declared")]
        public void Chain_RecordsDeclaredStage()
        {
            var chain = Chain(Bind<BeforeSaveOnlyPlugin>(PluginStage.BeforeSave));

            Assert.True(chain.HasStage(PluginStage.BeforeSave));
            Assert.False(chain.HasStage(PluginStage.AfterSave));
            Assert.False(chain.HasStage(PluginStage.BeforeDelete));
            Assert.False(chain.HasStage(PluginStage.AfterDelete));
        }

        [Fact]
        [DisplayName("The chain returns, for each stage, the types that will run at that stage (what maintenance tools read)")]
        public void Chain_TypesForStage_ListsOnlyThatStage()
        {
            var chain = Chain(
                Bind<AfterSaveOnlyPlugin>(PluginStage.AfterSave),
                Bind<BeforeSaveOnlyPlugin>(PluginStage.BeforeSave));

            Assert.Equal([typeof(BeforeSaveOnlyPlugin)], chain.TypesForStage(PluginStage.BeforeSave));
            Assert.Equal([typeof(AfterSaveOnlyPlugin)], chain.TypesForStage(PluginStage.AfterSave));
        }

        [Fact]
        [DisplayName("Building the chain is rejected when the declared stage differs from the class's override, and the message names the stage the class actually overrides")]
        public void Create_DeclaredStageDisagreesWithOverride_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => Chain(Bind<BeforeSaveOnlyPlugin>(PluginStage.AfterSave)));

            Assert.Contains("Stage=\"AfterSave\"", ex.Message, StringComparison.Ordinal);
            Assert.Contains("overrides BeforeSave", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Building the chain is rejected when no Stage is declared (missing from a hand-written file), and the message spells out the correct value")]
        public void Create_NoStageDeclared_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => Chain(Bind<BeforeSaveOnlyPlugin>(PluginStage.None)));

            Assert.Contains("with no Stage", ex.Message, StringComparison.Ordinal);
            Assert.Contains("declare Stage=\"BeforeSave\"", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Building the chain is rejected when a class overrides two stages (a plugin binds to exactly one stage)")]
        public void Create_TypeOverridesTwoStages_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => Chain(Bind<BothSaveStagesPlugin>(PluginStage.BeforeSave)));

            Assert.Contains("overrides BeforeSave and AfterSave", ex.Message, StringComparison.Ordinal);
            Assert.Contains("one class per stage", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Building the chain is rejected for a plugin that overrides no stage (binding it would do nothing)")]
        public void Create_TypeOverridesNothing_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => Chain(Bind<NoStagePlugin>(PluginStage.BeforeSave)));

            Assert.Contains("overrides no stage", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An empty chain constructs no instance and does not affect the pipeline")]
        public void EmptyChain_RunsNothing()
        {
            RecordingPlugin.Reset();
            var runner = CreateRunner();

            runner.RunBeforeSave(null!);
            runner.RunAfterSave(null!);

            Assert.True(FormPluginChain.Empty.IsEmpty);
            Assert.Equal(0, RecordingPlugin.ConstructedCount);
        }

        [Fact]
        [DisplayName("Several plugins run in declaration order")]
        public void Run_ExecutesInDeclarationOrder()
        {
            RecordingPlugin.Reset();
            var runner = CreateRunner(
                Bind<FirstPlugin>(PluginStage.BeforeSave),
                Bind<SecondPlugin>(PluginStage.BeforeSave));

            runner.RunBeforeSave(null!);

            Assert.Equal(["First.BeforeSave", "Second.BeforeSave"], RecordingPlugin.Calls);
        }

        [Fact]
        [DisplayName("A plugin bound to another stage is not called")]
        public void Run_SkipsPluginsBoundToAnotherStage()
        {
            RecordingPlugin.Reset();
            var runner = CreateRunner(
                Bind<BeforeSaveOnlyPlugin>(PluginStage.BeforeSave),
                Bind<AfterSaveOnlyPlugin>(PluginStage.AfterSave));

            runner.RunBeforeSave(null!);

            Assert.Equal(["BeforeSaveOnly.BeforeSave"], RecordingPlugin.Calls);
        }

        [Fact]
        [DisplayName("Instances are constructed on demand: running save does not construct a plugin bound only to a delete stage")]
        public void Run_ConstructsOnlyThePluginsOfTheStageBeingRun()
        {
            RecordingPlugin.Reset();
            var runner = CreateRunner(
                Bind<BeforeSaveOnlyPlugin>(PluginStage.BeforeSave),
                Bind<BeforeDeleteOnlyPlugin>(PluginStage.BeforeDelete));

            runner.RunBeforeSave(null!);

            // The old design built the whole chain at once so that later stages would find the same object. Now that a plugin binds to one stage,
            // that reason is gone, and a delete-only plugin should not be constructed during a save.
            Assert.Equal(1, RecordingPlugin.ConstructedCount);
            Assert.Equal(["BeforeSaveOnly.BeforeSave"], RecordingPlugin.Calls);
        }

        [Fact]
        [DisplayName("A chain that is never used constructs no instance")]
        public void Run_UnusedStage_ConstructsNothing()
        {
            RecordingPlugin.Reset();
            var runner = CreateRunner(Bind<BeforeSaveOnlyPlugin>(PluginStage.BeforeSave));

            runner.RunBeforeDelete(null!);

            Assert.Equal(0, RecordingPlugin.ConstructedCount);
        }

        [Fact]
        [DisplayName("The same plugin is constructed only once within one operation")]
        public void Run_SameOperation_ConstructsEachPluginOnce()
        {
            RecordingPlugin.Reset();
            var runner = CreateRunner(Bind<BeforeSaveOnlyPlugin>(PluginStage.BeforeSave));

            runner.RunBeforeSave(null!);
            runner.RunBeforeSave(null!);

            Assert.Equal(1, RecordingPlugin.ConstructedCount);
        }

        [Fact]
        [DisplayName("Different operations get different instances, so state does not leak into the next call")]
        public void Run_DifferentOperations_DoNotShareInstances()
        {
            RecordingPlugin.Reset();
            var chain = Chain(Bind<CountingPlugin>(PluginStage.BeforeSave));

            chain.CreateRunner(_ctx, Guid.NewGuid(), "Order").RunBeforeSave(null!);
            chain.CreateRunner(_ctx, Guid.NewGuid(), "Order").RunBeforeSave(null!);

            Assert.Equal(2, RecordingPlugin.ConstructedCount);
            // Both calls report seen=1: the second instance did not inherit the first one's field values.
            Assert.Equal(["Counting.BeforeSave(seen=1)", "Counting.BeforeSave(seen=1)"], RecordingPlugin.Calls);
        }

        [Fact]
        [DisplayName("A plugin can declare its own injected dependencies beyond the three positional parameters (ActivatorUtilities)")]
        public void Run_ConstructsWithInjectedDependencies()
        {
            RecordingPlugin.Reset();
            var runner = CreateRunner(Bind<InjectedPlugin>(PluginStage.BeforeSave));

            runner.RunBeforeSave(null!);

            // The first three are still positional and the fourth is resolved from the container; on-demand construction did not change this.
            Assert.Equal(["Injected.BeforeSave(progId=Order, injected=yes)"], RecordingPlugin.Calls);
        }

        // ---- Test plugins ----

        /// <summary>A shared call log, isolated between tests with <see cref="Reset"/>.</summary>
        public abstract class RecordingPlugin : FormBusinessPlugin
        {
            protected RecordingPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId)
            {
                ConstructedCount++;
            }

            public static List<string> Calls { get; } = [];

            public static int ConstructedCount { get; private set; }

            public static void Reset()
            {
                Calls.Clear();
                ConstructedCount = 0;
            }

            protected static void Record(string call) => Calls.Add(call);
        }

        public sealed class BeforeSaveOnlyPlugin : RecordingPlugin
        {
            public BeforeSaveOnlyPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeSave(SaveContext context) => Record("BeforeSaveOnly.BeforeSave");
        }

        public sealed class AfterSaveOnlyPlugin : RecordingPlugin
        {
            public AfterSaveOnlyPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void AfterSave(SaveContext context) => Record("AfterSaveOnly.AfterSave");
        }

        public sealed class BeforeDeleteOnlyPlugin : RecordingPlugin
        {
            public BeforeDeleteOnlyPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeDelete(DeleteContext context) => Record("BeforeDeleteOnly.BeforeDelete");
        }

        /// <summary>Overrides two stages; used to verify that a plugin with more than one stage is rejected.</summary>
        public sealed class BothSaveStagesPlugin : RecordingPlugin
        {
            public BothSaveStagesPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeSave(SaveContext context) => Record("Both.BeforeSave");

            public override void AfterSave(SaveContext context) => Record("Both.AfterSave");
        }

        /// <summary>Overrides nothing, so binding it would do nothing.</summary>
        public sealed class NoStagePlugin : RecordingPlugin
        {
            public NoStagePlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }
        }

        /// <summary>Counts with an instance field; used to verify that instances are not shared across operations.</summary>
        public sealed class CountingPlugin : RecordingPlugin
        {
            private int _seen;

            public CountingPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeSave(SaveContext context)
            {
                _seen++;
                Record($"Counting.BeforeSave(seen={_seen})");
            }
        }

        /// <summary>Needs a dependency resolved from the container in addition to the three positional parameters.</summary>
        public sealed class InjectedPlugin : RecordingPlugin
        {
            private readonly IDefineAccess _defineAccess;

            public InjectedPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId, IDefineAccess defineAccess)
                : base(ctx, accessToken, progId)
            {
                _defineAccess = defineAccess;
            }

            public override void BeforeSave(SaveContext context)
                => Record($"Injected.BeforeSave(progId={ProgId}, injected={(_defineAccess is null ? "no" : "yes")})");
        }

        public sealed class FirstPlugin : RecordingPlugin
        {
            public FirstPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeSave(SaveContext context) => Record("First.BeforeSave");
        }

        public sealed class SecondPlugin : RecordingPlugin
        {
            public SecondPlugin(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            public override void BeforeSave(SaveContext context) => Record("Second.BeforeSave");
        }
    }
}
