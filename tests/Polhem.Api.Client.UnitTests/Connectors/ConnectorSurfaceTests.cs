using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client.Connectors;
using Polhem.Definition;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// Guards the client connector layer against the server's action surface: every <c>*Actions</c> constant has
    /// an <c>&lt;Action&gt;Async</c> method on its connector, every connector action method maps back to a constant,
    /// and all of them share one shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The contract, message and business-object layers are tied together by the compiler and by
    /// <c>ActionSurfaceTests</c>, but nothing tied the connector to them: an action could exist on the server with no
    /// .NET method to call it, which is how <c>GetFormSchema</c>, <c>GetFormLayout</c> and <c>GetLanguage</c> went
    /// without one.
    /// </para>
    /// <para>
    /// The shape rules: public and <c>virtual</c> (test doubles subclass connectors and override single actions),
    /// returning <see cref="Task{TResult}"/>, with a trailing optional <see cref="CancellationToken"/>. The trailing
    /// token is what keeps a later optional parameter from being blocked by RS0027.
    /// </para>
    /// </remarks>
    public class ConnectorSurfaceTests
    {
        /// <summary>The constants class → connector type mapping for each axis. Add a row when adding an axis.</summary>
        private static readonly (Type Actions, Type Connector)[] s_axes =
        [
            (typeof(SystemActions), typeof(SystemApiConnector)),
            (typeof(FormActions), typeof(FormApiConnector)),
            (typeof(AuditLogActions), typeof(AuditLogApiConnector)),
        ];

        /// <summary>
        /// Actions every business object inherits from its base class. Their constants live in
        /// <see cref="SystemActions"/>, and the form connector exposes them too.
        /// </summary>
        private static readonly string[] s_inheritedActions =
        [
            SystemActions.ExecFunc,
            SystemActions.ExecFuncAnonymous,
        ];

        /// <summary>
        /// Actions deliberately without a connector method, keyed by connector and action, with the reason. Every
        /// action is covered today; an entry is added only with a reason a reviewer can check.
        /// </summary>
        private static readonly Dictionary<(Type Connector, string Action), string> s_allowlist = [];

        public static TheoryData<Type, string> DeclaredActions()
        {
            var data = new TheoryData<Type, string>();
            foreach (var (actions, connector) in s_axes)
            {
                foreach (var action in ActionNames(actions))
                {
                    data.Add(connector, action);
                }
            }
            foreach (var action in s_inheritedActions)
            {
                data.Add(typeof(FormApiConnector), action);
            }
            return data;
        }

        public static TheoryData<Type, string> ConnectorActionMethods()
        {
            var data = new TheoryData<Type, string>();
            foreach (var (_, connector) in s_axes)
            {
                foreach (var method in ActionMethods(connector))
                {
                    data.Add(connector, method.Name);
                }
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(DeclaredActions))]
        [DisplayName("Every action constant has an <Action>Async method on its connector unless allowlisted")]
        public void Action_HasConnectorMethod(Type connector, string action)
        {
            var method = FindActionMethod(connector, action);

            if (s_allowlist.ContainsKey((connector, action)))
            {
                Assert.True(method == null,
                    $"{connector.Name}.{action}Async exists now; remove the stale allowlist entry.");
                return;
            }
            Assert.True(method != null, $"{connector.Name} has no public {action}Async method.");
        }

        [Theory]
        [MemberData(nameof(ConnectorActionMethods))]
        [DisplayName("Every connector action method maps to an action constant of its axis")]
        public void ConnectorMethod_MapsToActionConstant(Type connector, string methodName)
        {
            var axisActions = s_axes.Single(axis => axis.Connector == connector).Actions;
            var known = ActionNames(axisActions).ToHashSet(StringComparer.Ordinal);
            if (connector == typeof(FormApiConnector))
            {
                known.UnionWith(s_inheritedActions);
            }

            string action = methodName[..^"Async".Length];
            Assert.True(known.Contains(action),
                $"{connector.Name}.{methodName} matches no constant in {axisActions.Name}.");
        }

        [Theory]
        [MemberData(nameof(ConnectorActionMethods))]
        [DisplayName("Every connector action method is virtual, returns Task<T> and takes a trailing optional CancellationToken")]
        public void ConnectorMethod_HasUniformShape(Type connector, string methodName)
        {
            var method = connector.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;

            Assert.True(method.IsVirtual && !method.IsFinal, $"{connector.Name}.{methodName} is not virtual.");
            Assert.True(method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
                $"{connector.Name}.{methodName} does not return Task<T>.");

            var last = method.GetParameters()[^1];
            Assert.Equal(typeof(CancellationToken), last.ParameterType);
            Assert.True(last.HasDefaultValue, $"{connector.Name}.{methodName} has a CancellationToken without a default.");
        }

        [Fact]
        [DisplayName("The surface tests see methods at all, so a broken filter cannot pass them vacuously")]
        public void ConnectorActionMethods_AreFound()
        {
            foreach (var (_, connector) in s_axes)
            {
                Assert.NotEmpty(ActionMethods(connector));
            }
        }

        private static IEnumerable<string> ActionNames(Type actions)
            => actions.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!);

        private static MethodInfo? FindActionMethod(Type connector, string action)
            => ActionMethods(connector).SingleOrDefault(method => method.Name == action + "Async");

        /// <summary>
        /// The public instance methods a connector declares whose names end in <c>Async</c>, except the plumbing:
        /// <c>ExecuteAsync</c> sends any action by name, and <c>InitializeAsync</c> applies a fetched configuration to
        /// this process rather than being an action of its own.
        /// </summary>
        private static List<MethodInfo> ActionMethods(Type connector)
            => connector.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.Name.EndsWith("Async", StringComparison.Ordinal)
                    && method.Name != "ExecuteAsync"
                    && method.Name != nameof(SystemApiConnector.InitializeAsync))
                .ToList();
    }
}
