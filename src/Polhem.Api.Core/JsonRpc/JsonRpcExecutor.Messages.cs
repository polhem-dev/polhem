using System.Data.Common;
using System.Globalization;
using Polhem.Base.Exceptions;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;

namespace Polhem.Api.Core.JsonRpc
{
    public partial class JsonRpcExecutor
    {
        /// <summary>
        /// Gets or sets the language service that translates the messages of the framework's
        /// user-facing exceptions before they leave the server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>AddPolhemFramework</c> assigns the registered <see cref="ILanguageService"/>, which
        /// answers from the deployment's language resources first and from the translations shipped
        /// with the framework second (<see cref="FrameworkLanguageService"/>). Left null, messages
        /// travel in English.
        /// </para>
        /// <para>
        /// Only an exception implementing <see cref="ILocalizableMessage"/> with a key, and whose
        /// message the error contract lets through verbatim, is translated. The key resolves in the
        /// session's culture through the language fall-back chain; a call with no session — a failed
        /// sign-in — starts the chain at the default language. A key no culture translates, or a
        /// translation whose placeholders the arguments cannot fill, sends the English text.
        /// </para>
        /// </remarks>
        public ILanguageService? LanguageService { get; set; }

        /// <summary>
        /// Returns the message to send for <paramref name="ex"/>: its translation when one applies,
        /// else <paramref name="message"/> unchanged.
        /// </summary>
        /// <param name="ex">The unwrapped exception.</param>
        /// <param name="message">The message <see cref="MapException"/> chose.</param>
        internal string LocalizeMessage(Exception ex, string message)
        {
            if (LanguageService is null
                || ex is not ILocalizableMessage localizable
                || string.IsNullOrEmpty(localizable.MessageKey)
                || !JsonRpcErrorContract.TryGetCode(ex, out _, out var fixedMessage)
                || fixedMessage != null)
            {
                return message;
            }

            var session = FindSession(ex);
            (string @namespace, string subKey) = LanguageKey.Split(localizable.MessageKey);
            if (!LanguageService.TryResolveLangText(session?.CustomizeId ?? string.Empty, session?.Culture ?? string.Empty,
                    @namespace, subKey, out string template))
            {
                return message;
            }

            try
            {
                return localizable.MessageArguments.Count == 0
                    ? template
                    : string.Format(CultureInfo.InvariantCulture, template, [.. localizable.MessageArguments]);
            }
            catch (FormatException)
            {
                // A translation that names more placeholders than the throw site supplies must not
                // turn a clear refusal into a server error; the English text is still correct.
                return message;
            }
        }

        /// <summary>
        /// Finds the caller's session for the culture of its messages, or <c>null</c> when there is
        /// none to find.
        /// </summary>
        private SessionInfo? FindSession(Exception ex)
        {
            // An authentication failure means there is no usable session; looking it up again would
            // repeat the failure on the error path.
            if (_sessionService is null || AccessToken == Guid.Empty || ex is AuthenticationRequiredException)
                return null;

            try
            {
                return _sessionService.Get(AccessToken);
            }
            catch (Exception lookupEx) when (lookupEx is DbException or InvalidOperationException or TimeoutException)
            {
                // The error being reported matters more than its language: a session store that
                // cannot answer here leaves the message in the default language.
                return null;
            }
        }
    }
}
