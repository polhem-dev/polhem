using System.ComponentModel;
using System.Reflection;
using Polhem.Business.AuditLog;
using Polhem.Business.Form;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Definition.Attributes;

namespace Polhem.Business.UnitTests.Contracts
{
    /// <summary>
    /// Guards the <b>action</b> symmetry of the contract axis: every <c>*Actions</c> constant and the public methods of the matching BO must
    /// correspond one to one, and each method must be a valid API entry point (a single <c>BusinessArgs</c> parameter, a return type of
    /// <c>BusinessResult</c>, covered by <c>[ApiAccessControl]</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the type symmetry gates cannot catch this.</b> <c>ApiContractPairingTests</c> and
    /// <c>BusinessContractPairingTests</c> guard whether a type has a paired contract interface, which assumes the type
    /// exists. But a JSON-RPC method is a <b>string</b>: <c>JsonRpcExecutor.GetMethod</c> takes the
    /// <c>action</c> and calls <c>GetType().GetMethod(action)</c> directly. With one letter wrong in a constant, both pairing
    /// tests stay green, and the symptom is a runtime <c>MissingMethodException</c>.
    /// </para>
    /// <para>
    /// The reverse is the same: a BO method with <c>[ApiAccessControl]</c> but no registered constant is already a callable
    /// API surface, yet callers can only use a magic string, and neither the compiler nor the existing tests say anything.
    /// </para>
    /// <para>
    /// <c>ExecFunc</c> / <c>ExecFuncAnonymous</c> are declared on the <see cref="BusinessObject"/> base and
    /// inherited by every axis, and their constants are registered in <see cref="SystemActions"/>, so the reverse check of every axis accepts these two names.
    /// </para>
    /// </remarks>
    public class ActionSurfaceTests
    {
        private static readonly string[] s_inheritedActions =
        [
            SystemActions.ExecFunc,
            SystemActions.ExecFuncAnonymous,
        ];

        /// <summary>
        /// The constants class → BO type mapping for each axis. Add a row when adding an axis.
        /// </summary>
        private static readonly (Type Actions, Type BusinessObject)[] s_axes =
        [
            (typeof(SystemActions), typeof(SystemBusinessObject)),
            (typeof(FormActions), typeof(FormBusinessObject)),
            (typeof(LogActions), typeof(LogBusinessObject)),
        ];

        /// <summary>
        /// Expands into one (BO type, action name) case each, so a failure message names the action directly.
        /// </summary>
        public static TheoryData<Type, string> DeclaredActions()
        {
            var data = new TheoryData<Type, string>();
            foreach (var (actions, businessObject) in s_axes)
            {
                foreach (var action in ActionNames(actions))
                {
                    data.Add(businessObject, action);
                }
            }
            return data;
        }

        /// <summary>
        /// Expands into one (BO type, method name) case each, covering every public method on that BO covered by
        /// <c>[ApiAccessControl]</c>.
        /// </summary>
        public static TheoryData<Type, string> ExposedMethods()
        {
            var data = new TheoryData<Type, string>();
            foreach (var (_, businessObject) in s_axes)
            {
                foreach (var method in ApiMethods(businessObject))
                {
                    data.Add(businessObject, method.Name);
                }
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(DeclaredActions))]
        [DisplayName("Every action constant maps to a valid API method on the BO")]
        public void DeclaredAction_HasMatchingApiMethod(Type businessObjectType, string action)
        {
            // The same resolution path as `JsonRpcExecutor.GetMethod`: the public method is looked up by the action string.
            var method = businessObjectType.GetMethod(action);
            Assert.True(method != null,
                $"{businessObjectType.Name} has no public method named '{action}'. " +
                "JsonRpcExecutor reflects the method directly from the action string, so a mismatch is a runtime MissingMethodException.");

            var parameters = method!.GetParameters();
            Assert.True(parameters.Length == 1,
                $"{businessObjectType.Name}.{action} should have exactly one parameter, but has {parameters.Length}. " +
                "The executor passes only a single args object.");

            Assert.True(typeof(BusinessArgs).IsAssignableFrom(parameters[0].ParameterType),
                $"The parameter type of {businessObjectType.Name}.{action} should inherit BusinessArgs, but is " +
                $"{parameters[0].ParameterType.Name}.");

            Assert.True(typeof(BusinessResult).IsAssignableFrom(UnwrapTask(method.ReturnType)),
                $"The return type of {businessObjectType.Name}.{action} should inherit BusinessResult, but is " +
                $"{method.ReturnType.Name}. ApiOutputConverter converts outbound results by the XxxResult → XxxResponse naming convention.");

            Assert.True(FindAccessAttribute(method) != null,
                $"{businessObjectType.Name}.{action} is not covered by [ApiAccessControl]. " +
                "ApiAccessValidator rejects anything undeclared, so this action throws UnauthorizedAccessException at runtime.");
        }

        [Theory]
        [MemberData(nameof(ExposedMethods))]
        [DisplayName("Every method the BO exposes is registered as an action constant")]
        public void ExposedMethod_IsDeclaredAsAction(Type businessObjectType, string methodName)
        {
            var actions = s_axes.Single(x => x.BusinessObject == businessObjectType).Actions;
            var declared = ActionNames(actions).Concat(s_inheritedActions);

            Assert.True(declared.Contains(methodName, StringComparer.Ordinal),
                $"{businessObjectType.Name}.{methodName} carries [ApiAccessControl] (so it is already a callable API surface), " +
                $"but {actions.Name} has no matching constant. Callers can only call it with a magic string.");
        }

        [Fact]
        [DisplayName("Neither direction's case enumeration is empty")]
        public void Enumerations_AreNotEmpty()
        {
            // Prevents the two theories above from passing with zero cases because a reflection condition is wrong.
            Assert.NotEmpty(DeclaredActions());
            Assert.NotEmpty(ExposedMethods());
        }

        /// <summary>
        /// Returns the values of every <c>public const string</c> in a constants class.
        /// </summary>
        private static IEnumerable<string> ActionNames(Type actionsType)
        {
            return actionsType
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!)
                .OrderBy(x => x, StringComparer.Ordinal);
        }

        /// <summary>
        /// Returns every public instance method on the BO covered by <c>[ApiAccessControl]</c>.
        /// </summary>
        private static IEnumerable<MethodInfo> ApiMethods(Type businessObjectType)
        {
            return businessObjectType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                            | BindingFlags.FlattenHierarchy)
                .Where(m => !m.IsSpecialName)
                .Where(m => FindAccessAttribute(m) != null)
                .OrderBy(m => m.Name, StringComparer.Ordinal);
        }

        /// <summary>
        /// The same three-step lookup as <c>ApiAccessValidator.FindAccessAttribute</c>: the method itself → the overridden
        /// base method → the declaring type. It has to be copied rather than referenced because that one is private. The test
        /// bears the cost of the copy, which is better than the test using a different rule.
        /// </summary>
        private static ApiAccessControlAttribute? FindAccessAttribute(MethodInfo method)
        {
            var attr = method.GetCustomAttribute<ApiAccessControlAttribute>();
            if (attr != null) { return attr; }

            var baseMethod = method.GetBaseDefinition();
            if (baseMethod != method)
            {
                attr = baseMethod.GetCustomAttribute<ApiAccessControlAttribute>();
                if (attr != null) { return attr; }
            }

            return method.DeclaringType?.GetCustomAttribute<ApiAccessControlAttribute>();
        }

        /// <summary>
        /// Returns the actual result type: an async method returns <c>Task&lt;T&gt;</c>, and the executor awaits it and takes Result.
        /// </summary>
        private static Type UnwrapTask(Type returnType)
        {
            return returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
                ? returnType.GetGenericArguments()[0]
                : returnType;
        }
    }
}
