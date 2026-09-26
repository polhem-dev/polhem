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
    /// <b>Two layers guard separately, from different sources.</b> <see cref="ApiAuthorizationValidator"/>, at the
    /// HTTP layer, decides from a hard-coded list of method names whether an <c>Authorization</c> header is required.
    /// <see cref="ApiAccessValidator"/>, at the BO layer, reads the attribute to decide whether the token is really
    /// validated. What happens when the two disagree is pinned in <see cref="AnonymousMethods_HttpGate_IsPinned"/>:
    /// a method marked <c>Anonymous</c> but missing from the HTTP-layer list needs a header, yet that header
    /// <b>is only checked for whether it parses as a Guid</b>, so any Guid passes. That check is not authentication.
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
        [DisplayName("The actual HTTP-layer gate for anonymous methods: only Ping and Login really need no header")]
        public void AnonymousMethods_HttpGate_IsPinned()
        {
            var validator = new ApiAuthorizationValidator();

            // Only a method that passes without an `Authorization` header truly needs no header.
            static ApiAuthorizationContext WithoutHeader(string method) => new()
            {
                Method = method,
                ApiKey = "present",          // With the key gate disabled, only non-emptiness is checked.
                Authorization = string.Empty,
            };

            Assert.True(validator.Validate(WithoutHeader($"{SysProgIds.System}.Ping")).IsValid);
            Assert.True(validator.Validate(WithoutHeader($"{SysProgIds.System}.Login")).IsValid);

            // The other methods marked Anonymous still require a header at the HTTP layer.
            string[] requireHeader =
            [
                $"{SysProgIds.System}.GetCommonConfiguration",
                $"{SysProgIds.System}.ExecFuncAnonymous",
            ];
            foreach (var method in requireHeader)
                Assert.False(validator.Validate(WithoutHeader(method)).IsValid, method);

            // WARNING: That header is only checked for whether it parses as a Guid, so any Guid passes, and the BO
            // layer does not validate it either because the attribute is Anonymous. These methods are therefore
            // reachable anonymously in practice, and the header is not authentication. It is pinned here so that
            // anyone reading only the HTTP-layer allow list does not underestimate the attack surface.
            foreach (var method in requireHeader)
            {
                var result = validator.Validate(new ApiAuthorizationContext
                {
                    Method = method,
                    ApiKey = "present",
                    Authorization = $"Bearer {Guid.Empty}",
                });
                Assert.True(result.IsValid, method);
                Assert.Equal(Guid.Empty, result.AccessToken);
            }
        }
    }
}
