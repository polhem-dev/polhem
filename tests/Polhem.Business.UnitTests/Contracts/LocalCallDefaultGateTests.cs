using System.ComponentModel;
using System.Reflection;

namespace Polhem.Business.UnitTests.Contracts
{
    /// <summary>
    /// The <c>isLocalCall</c> default of every BO constructor must be <c>false</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A default of <c>true</c> means that constructing a BO directly, the only path that bypasses <c>ApiAccessValidator</c>,
    /// is treated as a trusted in-process call by default. The second line of defense of several methods is conditioned on <c>IsLocalCall</c>
    /// (minting an API key without being a deployment admin, granting deployment admin, writing server-only definitions),
    /// so those guards would all pass on the default path. **Only callers that deliberately write `isLocalCall: false` would be blocked,
    /// and they are the ones that least need blocking.**
    /// </para>
    /// <para>
    /// It scans every subclass by reflection rather than listing them by name, so a new BO family is covered automatically.
    /// That is exactly what was missing: each constructor declared its own default, and no mechanism required them to agree.
    /// </para>
    /// </remarks>
    public class LocalCallDefaultGateTests
    {
        [Fact]
        [DisplayName("Every BusinessObject constructor defaults isLocalCall to false")]
        public void EveryBusinessObjectConstructor_DefaultsIsLocalCallToFalse()
        {
            var boTypes = typeof(BusinessObject).Assembly.GetTypes()
                .Where(t => typeof(BusinessObject).IsAssignableFrom(t))
                .ToArray();

            // Guards against a vacuous pass: if the types cannot be loaded, the loop below never runs.
            Assert.Contains(typeof(BusinessObject), boTypes);

            var offenders = new List<string>();
            int checkedCount = 0;
            foreach (var type in boTypes)
            {
                foreach (var ctor in type.GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var parameter = ctor.GetParameters().FirstOrDefault(p => p.Name == "isLocalCall");
                    if (parameter is null || !parameter.HasDefaultValue) { continue; }

                    checkedCount++;
                    if (!Equals(parameter.DefaultValue, false))
                    {
                        offenders.Add($"{type.Name} (default {parameter.DefaultValue})");
                    }
                }
            }

            // A second guard against a vacuous pass: constructors with a default really were checked, rather than all skipped by a wrong condition.
            Assert.True(checkedCount > 0, "No constructor with an isLocalCall default was checked, so this gate checks nothing.");

            Assert.True(
                offenders.Count == 0,
                "These BO constructors default isLocalCall to true, so a caller that constructs them directly is treated by default as a trusted " +
                $"in-process call, bypassing the second line of defense conditioned on IsLocalCall: {string.Join(", ", offenders)}.");
        }
    }
}
