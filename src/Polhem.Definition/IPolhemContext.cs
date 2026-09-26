using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;

namespace Polhem.Definition
{
    /// <summary>
    /// Per-call context handed to business objects at construction time.
    /// Aggregates the cross-cutting services that virtually every BO method
    /// touches, plus an <see cref="IServiceProvider"/> escape hatch for rare
    /// per-method needs (e.g. login-only helpers).
    /// </summary>
    public interface IPolhemContext
    {
        /// <summary>The definition data access service.</summary>
        IDefineAccess DefineAccess { get; }

        /// <summary>The session-info access service.</summary>
        ISessionInfoService SessionInfoService { get; }

        /// <summary>The language resource service (localized text + enum lookup).</summary>
        ILanguageService LanguageService { get; }

        /// <summary>Factory for building business objects (used for BO-to-BO calls).</summary>
        IBusinessObjectFactory BoFactory { get; }

        /// <summary>
        /// Escape hatch for resolving services not in the typed core members.
        /// Use sparingly — reserved for rare per-method needs (e.g. login-only
        /// helpers used by <c>Login</c>). Greppable via
        /// <c>Services.GetService&lt;T&gt;</c> for audit.
        /// </summary>
        IServiceProvider Services { get; }
    }
}
