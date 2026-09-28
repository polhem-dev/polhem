using System.ComponentModel;
using System.Linq.Expressions;

namespace Polhem.Expressions.UnitTests
{
    /// <summary>
    /// Asserts that an interpreted expression is invoked through a delegate the runtime can create without dynamic
    /// code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On iOS every expression is interpreted, and <c>LambdaExpression.Compile</c> still has to hand back a delegate
    /// of the lambda's exact signature. <c>System.Linq.Expressions</c> builds it from a precompiled C# thunk when the
    /// signature has at most two parameters, and emits a <c>DynamicMethod</c> thunk otherwise. The iOS runtime cannot
    /// run that thunk: an order line's <c>quantity * unit_price * (1 - discount)</c> terminated the app with an
    /// <c>ExecutionEngineException</c> (measured on 2026-09-28).
    /// </para>
    /// <para>
    /// The desktop runtime chooses a thunk the same way, because it runs the same <c>System.Linq.Expressions</c>
    /// code, but it can JIT the emitted one, so the failure never shows here and neither does
    /// <c>-p:DynamicCodeSupport=false</c>. What does show is which kind of thunk was chosen: a delegate over a
    /// <c>DynamicMethod</c> has no declaring type. These tests force interpretation and inspect that.
    /// </para>
    /// </remarks>
    public class InterpretedInvokerGateTests
    {
        public static TheoryData<string, Dictionary<string, object?>, Type, object> Cases => new()
        {
            { "quantity * unit_price * (1 - discount)",
                new() { ["quantity"] = 405, ["unit_price"] = 12.5m, ["discount"] = 0.1m }, typeof(decimal), 4556.25m },
            { "a * b + c - d", new() { ["a"] = 2, ["b"] = 3.5m, ["c"] = 1L, ["d"] = 0.5m }, typeof(decimal), 7.5m },
            { "qty > 0 && price >= 0m && !IsNullOrEmpty(name)",
                new() { ["qty"] = 1, ["price"] = 2m, ["name"] = "n" }, typeof(bool), true },
            { "Math.Round(x, 2)", new() { ["x"] = 3.14159m }, typeof(decimal), 3.14m },
            { "d.AddDays(1) > d", new() { ["d"] = new DateOnly(2000, 1, 1) }, typeof(bool), true },
            { "name.ToUpper() + code.Trim()", new() { ["name"] = "ab", ["code"] = " c " }, typeof(string), "ABc" },
            { "Math.Max(1, 2)", new(), typeof(int), 2 },
        };

        [Theory]
        [MemberData(nameof(Cases))]
        [DisplayName("An interpreted expression evaluates correctly behind a thunk that needs no dynamic code")]
        public void Evaluate_Interpreted_UsesNoRuntimeGeneratedThunk(
            string expression, Dictionary<string, object?> variables, Type returnType, object expected)
        {
            var evaluator = new DynamicExpressoEvaluator(preferInterpretation: true);

            var result = evaluator.Evaluate(expression, variables, returnType);

            Assert.Equal(expected, result);
            var invoker = Assert.Single(evaluator.CompiledInvokers);
            Assert.False(IsRuntimeGeneratedThunk(invoker),
                $"`{expression}` is invoked through a DynamicMethod thunk ({invoker.Method}), which iOS cannot run.");
        }

        [Fact]
        [DisplayName("An interpreted lambda with three value-type parameters is detected as needing a runtime-generated thunk")]
        public void IsRuntimeGeneratedThunk_InterpretedThreeParameterLambda_ReturnsTrue()
        {
            // The positive control: the typed shape DynamicExpresso compiles for the order-line expression. If a
            // future runtime stops emitting a thunk for it, this fails, and the theory above no longer proves anything.
            var quantity = Expression.Parameter(typeof(int), "quantity");
            var unitPrice = Expression.Parameter(typeof(decimal), "unit_price");
            var discount = Expression.Parameter(typeof(decimal), "discount");
            var body = Expression.Multiply(
                Expression.Multiply(Expression.Convert(quantity, typeof(decimal)), unitPrice),
                Expression.Subtract(Expression.Constant(1m), discount));
            var typed = Expression.Lambda<Func<int, decimal, decimal, decimal>>(body, quantity, unitPrice, discount)
                .Compile(preferInterpretation: true);

            Assert.True(IsRuntimeGeneratedThunk(typed));
            Assert.Equal(4556.25m, typed(405, 12.5m, 0.1m));
        }

        private static bool IsRuntimeGeneratedThunk(Delegate invoker) => invoker.Method.DeclaringType is null;
    }
}
