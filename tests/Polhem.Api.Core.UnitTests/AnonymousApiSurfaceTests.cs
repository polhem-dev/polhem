using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Core.Authorization;
using Polhem.Api.Core.Validator;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Attributes;
using Polhem.Definition.Security;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Keeps "which API methods need no login" as an allow list where every entry is declared by name, and pins
    /// the gate those methods actually meet at the HTTP layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The methods that need no login are this framework's anonymous attack surface. They are declared with
    /// <see cref="ApiAccessRequirement.Anonymous"/>, which is only an attribute argument on a method: adding a
    /// method marked with it asks nobody for a reason. This allow list is that request.
    /// </para>
    /// <para>
    /// <b>One source decides.</b> <see cref="ApiAuthorizationValidator"/>, at the HTTP layer, used to decide from a
    /// hard-coded list of method names whether an <c>Authorization</c> header was required, and that list disagreed
    /// with the attribute: it demanded a header for <c>GetCommonConfiguration</c> and <c>ExecFuncAnonymous</c> and
    /// exempted a method that no longer existed. The header was only ever checked for whether it parsed as a Guid,
    /// so it was never authentication. Now a request without the header proceeds with the empty token and
    /// <see cref="ApiAccessValidator"/>, reading the attribute, is the only gate; <see cref="AnonymousMethods_HttpGate_IsPinned"/>
    /// pins that for every framework action.
    /// </para>
    /// </remarks>
    public class AnonymousApiSurfaceTests
    {
        /// <summary>
        /// The allow list of methods that need no login. The key is "DeclaringType.MethodName"; the value is why it
        /// may skip login.
        /// </summary>
        /// <remarks>
        /// Before adding an entry, ask: **what does someone who is not logged in learn from this response?** The
        /// response content is the attack surface, not the method name.
        /// </remarks>
        private static readonly Dictionary<string, string> s_anonymousAllowList = new(StringComparer.Ordinal)
        {
            ["SystemBusinessObject.Ping"] =
                "Connectivity probe. It must answer even when the database is unavailable, so it is exempt even from the API key; the framework version is revealed only after passing the key gate.",
            ["SystemBusinessObject.Login"] =
                "Login itself. Needing no login is inherent by definition; the API key is still required, because which application is attempting to log in is exactly what should be recorded.",
            ["SystemBusinessObject.GetCommonConfiguration"] =
                "The client startup flow needs it before login to decide the compression and encryption settings. The response is the deployment-level payload configuration and contains no data.",
            ["SystemBusinessObject.CreateSession"] =
                "Declared LocalOnly, so remote calls are rejected at the BO layer. It is not part of the anonymous attack surface; Anonymous only matters for in-process calls.",
            ["BusinessObject.ExecFuncAnonymous"] =
                "Lets an application plug in its own anonymous functions. **Its attack surface depends on how the application implements it**; the framework only requires Encoded or stronger transport.",
        };

        private static Dictionary<string, ApiAccessControlAttribute> AnonymousMethods()
        {
            return typeof(BusinessObject).Assembly.GetTypes()
                .Where(t => typeof(BusinessObject).IsAssignableFrom(t))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => !m.IsSpecialName)
                .Select(m => (Method: m, Attr: ApiAccessValidator.FindAccessControl(m)))
                .Where(x => x.Attr?.AccessRequirement == ApiAccessRequirement.Anonymous)
                .ToDictionary(
                    x => $"{x.Method.DeclaringType!.Name}.{x.Method.Name}",
                    x => x.Attr!,
                    StringComparer.Ordinal);
        }

        [Fact]
        [DisplayName("Every API method that needs no login is declared by name in the allow list with a reason")]
        public void AnonymousMethods_AreAllExplicitlyAllowListed()
        {
            var actual = AnonymousMethods();

            // Finding no anonymous method means the scan is broken (Login is always one). Without this, the checks below pass vacuously.
            Assert.NotEmpty(actual);

            var undeclared = actual.Keys.Where(k => !s_anonymousAllowList.ContainsKey(k)).ToList();
            Assert.True(undeclared.Count == 0,
                "The following methods are marked Anonymous but not declared in the allow list, which widens the anonymous attack surface without review:" +
                global::System.Environment.NewLine +
                string.Join(global::System.Environment.NewLine, undeclared) +
                global::System.Environment.NewLine +
                "When adding them to the allow list, state what someone who is not logged in learns from the response, not just why it is convenient.");

            var ghosts = s_anonymousAllowList.Keys.Where(k => !actual.ContainsKey(k)).ToList();
            Assert.True(ghosts.Count == 0,
                "The following methods are in the no-login allow list but no longer exist or are no longer Anonymous; remove them. " +
                "Ghost entries in the allow list make the next security inventory overestimate or misjudge the attack surface:" +
                global::System.Environment.NewLine +
                string.Join(global::System.Environment.NewLine, ghosts));

            foreach (var (name, reason) in s_anonymousAllowList)
                Assert.False(string.IsNullOrWhiteSpace(reason), $"The no-login reason for {name} must not be empty.");
        }

        [Fact]
        [DisplayName("Without an Authorization header every framework action reaches the access check, which admits exactly the Anonymous ones")]
        public void AnonymousMethods_HttpGate_IsPinned()
        {
            var validator = new ApiAuthorizationValidator();
            var actions = typeof(BusinessObject).Assembly.GetTypes()
                .Where(t => typeof(BusinessObject).IsAssignableFrom(t))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(Polhem.Api.Core.JsonRpc.JsonRpcExecutor.IsResolvableAction)
                .Select(m => (Method: m, Attr: ApiAccessValidator.FindAccessControl(m)))
                .Where(x => x.Attr != null && x.Attr.ProtectionLevel != ApiProtectionLevel.LocalOnly)
                .ToList();

            // Both kinds must be present, or the loop below proves nothing about one of them.
            Assert.Contains(actions, x => x.Attr!.AccessRequirement == ApiAccessRequirement.Anonymous);
            Assert.Contains(actions, x => x.Attr!.AccessRequirement == ApiAccessRequirement.Authenticated);

            foreach (var (method, attr) in actions)
            {
                var name = $"{method.DeclaringType!.Name}.{method.Name}";

                // The transport no longer tells the two kinds apart: no header means the empty token.
                var result = validator.Validate(new ApiAuthorizationContext
                {
                    Method = $"{SysProgIds.System}.{method.Name}",
                    ApiKey = "present",          // With the key gate disabled, only non-emptiness is checked.
                    Authorization = string.Empty,
                });
                Assert.True(result.IsValid, name);
                Assert.Equal(Guid.Empty, result.AccessToken);

                // The attribute decides, and only the attribute.
                var context = new ApiCallContext(result.AccessToken, isLocalCall: false, Polhem.Api.Core.Messages.PayloadFormat.Encrypted);
                var denied = Record.Exception(() => { ApiAccessValidator.ValidateAccess(method, context, new RejectAllTokens()); });
                if (attr!.AccessRequirement == ApiAccessRequirement.Anonymous)
                    Assert.True(denied == null, $"{name} is Anonymous but was refused without a token.");
                else
                    Assert.True(denied != null, $"{name} is Authenticated but was admitted without a token.");
            }
        }

        private sealed class RejectAllTokens : IAccessTokenValidator
        {
            public bool Validate(Guid accessToken) => false;
        }
    }
}
