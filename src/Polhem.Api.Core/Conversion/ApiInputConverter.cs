using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polhem.Api.Core.Json;
using Polhem.Core.Serialization;

namespace Polhem.Api.Core.Conversion
{
    /// <summary>
    /// Converts API request objects to BO parameter types by copying matching properties.
    /// Used when the Executor receives an API type (e.g., LoginRequest) but the BO method
    /// expects a different type (e.g., ILoginRequest or LoginArgs).
    /// </summary>
    internal static class ApiInputConverter
    {
        /// <summary>
        /// The options every <c>Plain</c> body is read with, on the server (requests) and on the client
        /// (<see cref="ApiOutputConverter.ConvertResultValue{T}"/>, responses).
        /// </summary>
        /// <remarks>
        /// <para>
        /// WARNING: the <c>DataTable</c>, <c>DataSet</c> and enum converters must match the ones
        /// <see cref="JsonCodec"/> writes with. A reader missing one does not fail: a table
        /// deserializes with no rows, an enum throws, and the call appears to succeed with empty data.
        /// Both directions share this one instance so the list cannot drift between them, which is how
        /// the response side lost its table rows before.
        /// </para>
        /// <para>
        /// <see cref="PlainValueJsonConverter"/> is what only this reader has: a <c>Plain</c> body
        /// carries <c>object</c>-typed members as bare values, and without it they arrive as
        /// <see cref="JsonElement"/>, which no database provider can bind as a parameter.
        /// </para>
        /// <para>
        /// Shared, never rebuilt per call: <see cref="JsonSerializerOptions"/> caches the contract it
        /// builds for each type.
        /// </para>
        /// </remarks>
        internal static JsonSerializerOptions PlainReadOptions { get; } = CreateReadOptions();

        // The property pairs a conversion copies depend only on the two types, and looking them up
        // by reflection on every call was most of the conversion's cost. The keys are the request,
        // args, result and response types the API dispatches, a set fixed by the compiled code.
        private static readonly ConcurrentDictionary<(Type Source, Type Target), (PropertyInfo Source, PropertyInfo Target)[]> s_copyPlans = new();

        private static JsonSerializerOptions CreateReadOptions()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new DataTableJsonConverter());
            options.Converters.Add(new DataSetJsonConverter());
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(PlainValueJsonConverter.Instance);
            return options;
        }

        /// <summary>
        /// Converts the source object to the specified target type by copying public properties with matching names.
        /// If the source is a <see cref="JsonElement"/> (from JSON deserialization), it is deserialized directly.
        /// </summary>
        /// <param name="source">The source object (typically an API request type).</param>
        /// <param name="targetType">The target type to convert to (typically a BO args type or interface).</param>
        /// <returns>A new instance of the target type with matching properties copied from the source.</returns>
        public static object? Convert(object source, Type targetType)
        {
            if (source == null) return null;

            // If the source is already assignable to the target type, return as-is
            if (targetType.IsInstanceOfType(source))
                return source;

            // Handle JsonElement (from JSON deserialization over HTTP).
            // PropertyNameCaseInsensitive is required because the framework serializes
            // with camelCase naming policy (see JsonCodec internal options).
            if (source is JsonElement element)
            {
                return element.Deserialize(targetType, PlainReadOptions);
            }

            // If the target is an interface, we cannot create an instance directly
            if (targetType.IsInterface || targetType.IsAbstract)
                return source;

            var target = Activator.CreateInstance(targetType)!;
            var plan = s_copyPlans.GetOrAdd((source.GetType(), targetType), BuildCopyPlan);
            foreach (var (sourceProp, targetProp) in plan)
            {
                targetProp.SetValue(target, sourceProp.GetValue(source));
            }

            return target;
        }

        /// <summary>
        /// Pairs every writable public instance property of the target with the readable source
        /// property of the same name whose type it can hold.
        /// </summary>
        private static (PropertyInfo Source, PropertyInfo Target)[] BuildCopyPlan((Type Source, Type Target) types)
        {
            var plan = new List<(PropertyInfo Source, PropertyInfo Target)>();
            foreach (var targetProp in types.Target.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!targetProp.CanWrite) continue;

                var sourceProp = types.Source.GetProperty(targetProp.Name, BindingFlags.Public | BindingFlags.Instance);
                if (sourceProp != null && sourceProp.CanRead && targetProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                {
                    plan.Add((sourceProp, targetProp));
                }
            }
            return plan.ToArray();
        }
    }
}
