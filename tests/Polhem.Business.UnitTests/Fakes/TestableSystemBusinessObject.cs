using Polhem.Business.System;
using Polhem.Definition;

namespace Polhem.Business.UnitTests.Fakes
{
    /// <summary>
    /// A test subclass of <see cref="SystemBusinessObject"/> that lets the test set what <c>AuthenticateUser</c> returns,
    /// so unit tests can run the Login flow through its success or failure branch.
    /// </summary>
    public class TestableSystemBusinessObject : SystemBusinessObject
    {
        private readonly Func<LoginArgs, (bool Authenticated, string UserName)> _authenticator;

        public TestableSystemBusinessObject(
            IBusinessObjectContext ctx,
            Guid accessToken,
            Func<LoginArgs, (bool Authenticated, string UserName)> authenticator,
            bool isLocalCall = true)
            : base(ctx, accessToken, SysProgIds.System, isLocalCall)
        {
            _authenticator = authenticator;
        }

        protected override bool AuthenticateUser(LoginArgs args, out string userName)
        {
            var (authenticated, name) = _authenticator(args);
            userName = name;
            return authenticated;
        }
    }
}
