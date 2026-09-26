using Polhem.Definition.Collections;
using Polhem.Definition;

namespace Polhem.Business.UnitTests.Fakes
{
    /// <summary>
    /// A test subclass of <see cref="BusinessObject"/> that verifies <see cref="BusinessObject.ExecFunc"/>
    /// and <see cref="BusinessObject.ExecFuncAnonymous"/> dispatch to the DoExecFunc* overrides correctly.
    /// </summary>
    public class TestableBusinessObject : BusinessObject
    {
        public TestableBusinessObject(IPolhemContext ctx, Guid accessToken, bool isLocalCall = true)
            : base(ctx, accessToken, "TestProg", isLocalCall)
        {
        }

        public int ExecFuncCallCount { get; private set; }
        public int ExecFuncAnonymousCallCount { get; private set; }
        public ExecFuncArgs? LastArgs { get; private set; }

        protected override void DoExecFunc(ExecFuncArgs args, ExecFuncResult result)
        {
            ExecFuncCallCount++;
            LastArgs = args;
            result.Parameters.Add("Marker", "DoExecFunc");
        }

        protected override void DoExecFuncAnonymous(ExecFuncArgs args, ExecFuncResult result)
        {
            ExecFuncAnonymousCallCount++;
            LastArgs = args;
            result.Parameters.Add("Marker", "DoExecFuncAnonymous");
        }
    }

    /// <summary>
    /// A test class that overrides no DoExecFunc* method, to verify that the empty base implementation does not throw.
    /// </summary>
    public class BareBusinessObject : BusinessObject
    {
        public BareBusinessObject(IPolhemContext ctx, Guid accessToken, bool isLocalCall = true)
            : base(ctx, accessToken, "TestProg", isLocalCall)
        {
        }
    }
}
