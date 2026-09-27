using Polhem.Definition.Collections;
using Polhem.Business.Attributes;
using Polhem.Definition.Security;

namespace Polhem.Business.UnitTests.Fakes
{
    /// <summary>
    /// A test ExecFunc handler with methods covering the various <see cref="ExecFuncAccessControlAttribute"/> scenarios.
    /// </summary>
    public class FakeExecFuncHandler : IExecFuncHandler
    {
        /// <summary>
        /// Marked Anonymous.
        /// </summary>
        [ExecFuncAccessControl(ApiAccessRequirement.Anonymous)]
        public static void Anonymous(ExecFuncArgs args, ExecFuncResult result)
        {
            result.Parameters.Add("Called", "Anonymous");
            result.Parameters.Add("FuncId", args.FuncId);
        }

        /// <summary>
        /// Marked Authenticated.
        /// </summary>
        [ExecFuncAccessControl(ApiAccessRequirement.Authenticated)]
        public static void Authenticated(ExecFuncArgs args, ExecFuncResult result)
        {
            result.Parameters.Add("Called", "Authenticated");
        }

        /// <summary>
        /// Not marked with the attribute. Dispatch is fail-closed, so it is rejected whether or not the caller is authenticated.
        /// </summary>
        public static void NoAttribute(ExecFuncArgs args, ExecFuncResult result)
        {
            result.Parameters.Add("Called", "NoAttribute");
        }

        /// <summary>
        /// Marked LocalOnly, so only a local (in-process) call can reach it.
        /// </summary>
        [ExecFuncAccessControl(ApiAccessRequirement.Authenticated, LocalOnly = true)]
        public static void LocalOnly(ExecFuncArgs args, ExecFuncResult result)
        {
            result.Parameters.Add("Called", "LocalOnly");
        }

        /// <summary>
        /// Tests exception unwrapping: reflection wraps the original exception in <see cref="global::System.Reflection.TargetInvocationException"/>,
        /// and <see cref="Polhem.Base.Exceptions.ExceptionExtensions.Unwrap"/> restores the original type.
        /// </summary>
        [ExecFuncAccessControl(ApiAccessRequirement.Anonymous)]
        public static void Throws(ExecFuncArgs args, ExecFuncResult result)
        {
            throw new InvalidOperationException("fake-inner-exception");
        }
    }
}
