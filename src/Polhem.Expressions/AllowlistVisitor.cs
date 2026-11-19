using System.Linq.Expressions;
using System.Reflection;
using Polhem.Core.Expressions;

namespace Polhem.Expressions
{
    /// <summary>
    /// Walks a parsed expression and collects what it uses outside <see cref="ExpressionAllowlist"/>.
    /// </summary>
    /// <remarks>
    /// Node kinds are allowed one by one; any kind not handled here is a violation, so a construct the parser starts
    /// to produce later is rejected until someone decides it is safe.
    /// </remarks>
    internal sealed class AllowlistVisitor : ExpressionVisitor
    {
        private readonly string _text;
        private readonly IReadOnlySet<Delegate> _helpers;
        private readonly List<ExpressionViolation> _violations = [];
        private int _concatenations;

        private AllowlistVisitor(string text, IReadOnlySet<Delegate> helpers)
        {
            _text = text;
            _helpers = helpers;
        }

        /// <summary>
        /// Returns the violations in <paramref name="body"/>, the parsed form of <paramref name="text"/>, ordered by
        /// where they are written.
        /// </summary>
        /// <param name="body">The parsed expression.</param>
        /// <param name="text">The expression as written, used to place each violation.</param>
        /// <param name="helpers">The helper function delegates the engine registers; calling one is allowed.</param>
        public static IReadOnlyList<ExpressionViolation> Find(Expression body, string text, IReadOnlySet<Delegate> helpers)
        {
            var visitor = new AllowlistVisitor(text, helpers);
            visitor.Visit(body);
            // In the order they are written, so the first one shown is the leftmost thing to fix; a finding with no
            // place in the text (an operator, a type) comes last.
            return visitor._violations.OrderBy(v => v.Position ?? int.MaxValue).ToList();
        }

        public override Expression? Visit(Expression? node)
        {
            if (node is null) { return null; }
            switch (node.NodeType)
            {
                case ExpressionType.Parameter:
                case ExpressionType.Constant:
                case ExpressionType.Conditional:
                case ExpressionType.MemberAccess:
                case ExpressionType.Call:
                case ExpressionType.New:
                case ExpressionType.NewArrayInit:
                case ExpressionType.Invoke:
                case ExpressionType.Default:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.Not:
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.UnaryPlus:
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                case ExpressionType.ExclusiveOr:
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.Coalesce:
                    return base.Visit(node);
                default:
                    Report($"The construct '{node.NodeType}' is not allowed in an expression.", null);
                    return node;
            }
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is Delegate helper && _helpers.Contains(helper)) { return node; }
            if (node.Value is not null && !ExpressionAllowlist.IsValueType(node.Value.GetType()))
            {
                Report($"A value of type '{node.Value.GetType().Name}' is not allowed in an expression.",
                    node.Value is Type ? ExpressionAllowlist.FindWord(_text, "typeof") : null);
            }
            return node;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            CheckType(node.Type, node.Name);
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            CheckMember(node.Member);
            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // A helper function may be called through its delegate's Invoke method.
            if (node.Object is ConstantExpression { Value: Delegate helper } && _helpers.Contains(helper)
                && node.Method.Name == nameof(Action.Invoke))
            {
                CheckHelperCall(helper);
                Visit(node.Arguments);
                return node;
            }
            if (node.Method.DeclaringType == typeof(string) && node.Method.Name == nameof(string.Concat))
                CountConcatenation();
            CheckMember(node.Method);
            return base.VisitMethodCall(node);
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            if (node.Expression is ConstantExpression { Value: Delegate helper } && _helpers.Contains(helper))
            {
                CheckHelperCall(helper);
                Visit(node.Arguments);
                return node;
            }
            Report("Only the helper functions can be called by name in an expression.", null);
            return node;
        }

        protected override Expression VisitNew(NewExpression node)
        {
            if (node.Constructor is null || !ExpressionAllowlist.IsAllowed(node.Constructor))
                Report($"Creating a '{node.Type.Name}' is not allowed in an expression.", ExpressionAllowlist.FindWord(_text, "new"));
            return base.VisitNew(node);
        }

        protected override Expression VisitNewArray(NewArrayExpression node)
        {
            CheckType(node.Type, null);
            return base.VisitNewArray(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            CheckType(node.Type, null);
            CheckType(node.Operand.Type, null);
            if (node.Method is not null) { CheckMember(node.Method); }
            return base.VisitUnary(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.Method is { } method && method.DeclaringType == typeof(string) && method.Name == nameof(string.Concat))
                CountConcatenation();
            if (node.Method is not null) { CheckMember(node.Method); }
            return base.VisitBinary(node);
        }

        private void CheckHelperCall(Delegate helper)
        {
            if (ExpressionAllowlist.IsAllowedFunction(helper.Method.Name)) { return; }
            Report("RegexMatch is not allowed in an expression from an untrusted author.",
                ExpressionAllowlist.FindWord(_text, nameof(BusinessFunctions.RegexMatch)));
        }

        private void CountConcatenation()
        {
            _concatenations++;
            if (_concatenations > ExpressionAllowlist.MaxConcatenations)
            {
                Report(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"The expression joins text more than {ExpressionAllowlist.MaxConcatenations} times."), null);
            }
        }

        private void CheckMember(MemberInfo member)
        {
            if (ExpressionAllowlist.IsAllowed(member)) { return; }
            var owner = member.DeclaringType?.Name ?? "?";
            Report($"'{owner}.{member.Name}' is not allowed in an expression.", ExpressionAllowlist.FindWord(_text, member.Name));
        }

        private void CheckType(Type type, string? name)
        {
            if (ExpressionAllowlist.IsValueType(type)) { return; }
            int? position = name is not null ? ExpressionAllowlist.FindWord(_text, name)
                : typeof(Type).IsAssignableFrom(type) ? ExpressionAllowlist.FindWord(_text, "typeof")
                : null;
            Report($"A value of type '{type.Name}' is not allowed in an expression.", position);
        }

        private void Report(string message, int? position)
        {
            int index = _violations.FindIndex(v => v.Message == message);
            if (index < 0) { _violations.Add(new ExpressionViolation(message, position)); }
            else if (_violations[index].Position is null && position is not null)
                _violations[index] = new ExpressionViolation(message, position);
        }
    }
}
