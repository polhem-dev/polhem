using Polhem.Definition.Security;

namespace Polhem.Northwind.Blazor.Demo;

/// <summary>
/// A login attempt tracker that never locks an account out.
/// </summary>
/// <remarks>
/// The demo site publishes every account's password, so a lockout guards nothing there. What it would do is let one
/// visitor lock every other visitor out: the framework's tracker counts failures per user id, so five wrong passwords
/// for <c>demo</c> would close the account to everyone for the lockout period, again and again. Failed sign-ins are
/// still recorded in the login audit trail. Attempts through <c>/api</c> are slowed by its rate limit; attempts in a
/// Blazor connection run in process, as fast as anything else that connection does.
/// </remarks>
internal sealed class NoLockoutLoginAttemptTracker : ILoginAttemptTracker
{
    public bool IsLockedOut(string userId) => false;

    public void RecordFailure(string userId)
    {
        // Nothing to count: no number of failures locks an account on the demo site.
    }

    public void Reset(string userId)
    {
        // Nothing was counted, so there is nothing to clear.
    }
}
