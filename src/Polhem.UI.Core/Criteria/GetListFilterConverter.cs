using Polhem.Definition.Filters;

namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// Turns <see cref="QueryCriteria"/> into the filter of a form list request
    /// (<see cref="Polhem.Api.Contracts.Form.IFormApiConnector.GetListAsync"/>).
    /// </summary>
    /// <remarks>
    /// The server still checks every field the filter names and adds the user's record scope; this converter only
    /// shapes what the user entered.
    /// </remarks>
    public static class GetListFilterConverter
    {
        /// <summary>
        /// Creates the filter of a list request.
        /// </summary>
        /// <param name="criteria">The criteria.</param>
        /// <param name="today">Today in the user's time zone, which relative dates resolve against
        /// (<see cref="Polhem.Core.FrameworkClock.Today"/>).</param>
        /// <param name="firstDayOfWeek">The day a week starts on, for the week periods.</param>
        /// <returns>The filter, or <c>null</c> when no row has anything entered.</returns>
        /// <exception cref="InvalidOperationException">A required row has nothing entered.</exception>
        /// <remarks>
        /// A date-time range covers its end day whole: it becomes <c>&gt;=</c> the start day and <c>&lt;</c> the
        /// day after the end. These instants are <see cref="DateTimeKind.Unspecified"/>, so the request's time zone
        /// conversion reads them in the user's zone.
        /// </remarks>
        public static FilterNode? ToFilter(QueryCriteria criteria, DateOnly today, DayOfWeek firstDayOfWeek)
            => ToFilter(criteria, today, firstDayOfWeek, identity: null);

        /// <summary>
        /// Creates the filter of a list request, resolving <see cref="CriteriaValue.CurrentUser"/> to the signed-in
        /// user's row ids.
        /// </summary>
        /// <param name="criteria">The criteria.</param>
        /// <param name="today">Today in the user's time zone, which relative dates resolve against
        /// (<see cref="Polhem.Core.FrameworkClock.Today"/>).</param>
        /// <param name="firstDayOfWeek">The day a week starts on, for the week periods.</param>
        /// <param name="identity">The row ids that stand for the user (<see cref="IUserContext.Identity"/>);
        /// <c>null</c> when unknown.</param>
        /// <returns>The filter, or <c>null</c> when no row has anything entered.</returns>
        /// <exception cref="InvalidOperationException">A required row has nothing entered.</exception>
        /// <remarks>
        /// <see cref="CriteriaValue.CurrentUser"/> becomes every known id in <see cref="UserIdentity.RowIds"/>, as the
        /// record scope's <c>Own</c> strategy compares an owner column. With none known it adds no id, so a row
        /// holding only that value matches no record rather than every record.
        /// </remarks>
        public static FilterNode? ToFilter(QueryCriteria criteria, DateOnly today, DayOfWeek firstDayOfWeek, UserIdentity? identity)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            if (criteria.MissingRequired.FirstOrDefault() is { } missing)
                throw new InvalidOperationException($"The required condition on {missing.Field.FieldName} has nothing entered.");

            var nodes = new List<FilterNode>();
            foreach (var row in criteria.Rows.Where(r => r.HasValue))
                AddConditions(row, today, firstDayOfWeek, identity, nodes);
            return nodes.Count switch
            {
                0 => null,
                1 => nodes[0],
                _ => FilterGroup.All([.. nodes]),
            };
        }

        private static void AddConditions(CriteriaRow row, DateOnly today, DayOfWeek firstDayOfWeek, UserIdentity? identity,
            List<FilterNode> nodes)
        {
            string name = row.Field.FieldName;
            bool isInstant = row.Field.Kind == CriteriaFieldKind.DateTime;
            switch (row.Operator)
            {
                case CriteriaOperator.Contains:
                    nodes.Add(FilterCondition.Contains(name, (string)row.Value!));
                    break;
                case CriteriaOperator.StartsWith:
                    nodes.Add(FilterCondition.StartsWith(name, (string)row.Value!));
                    break;
                case CriteriaOperator.Equal when isInstant:
                    AddDays(name, (DateOnly)row.Value!, (DateOnly)row.Value!, nodes);
                    break;
                case CriteriaOperator.Equal:
                    nodes.Add(new FilterCondition(name, ComparisonOperator.Equal, row.Value!));
                    break;
                case CriteriaOperator.Range:
                    AddRange(row, today, firstDayOfWeek, isInstant, nodes);
                    break;
                case CriteriaOperator.In:
                    var values = ResolveValues(row, identity);
                    nodes.Add(values.Count == 1
                        ? new FilterCondition(name, ComparisonOperator.Equal, values[0])
                        : FilterCondition.In(name, values));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown criteria operator {row.Operator}.");
            }
        }

        private static List<object> ResolveValues(CriteriaRow row, UserIdentity? identity)
        {
            var values = new List<object>();
            foreach (var value in row.Values)
            {
                if (!value.IsCurrentUser)
                    values.Add(value.Value);
                else if (identity is not null)
                    values.AddRange(identity.RowIds.Cast<object>());
            }
            return [.. values.Distinct()];
        }

        private static void AddRange(CriteriaRow row, DateOnly today, DayOfWeek firstDayOfWeek, bool isInstant, List<FilterNode> nodes)
        {
            string name = row.Field.FieldName;
            object? from = row.From;
            object? to = row.To;
            if (row.RelativeDate is { } relative)
                (from, to) = relative.Resolve(today, firstDayOfWeek);

            if (isInstant)
            {
                AddDays(name, (DateOnly?)from, (DateOnly?)to, nodes);
                return;
            }
            if (from is not null)
                nodes.Add(new FilterCondition(name, ComparisonOperator.GreaterThanOrEqual, from));
            if (to is not null)
                nodes.Add(new FilterCondition(name, ComparisonOperator.LessThanOrEqual, to));
        }

        private static void AddDays(string name, DateOnly? firstDay, DateOnly? lastDay, List<FilterNode> nodes)
        {
            if (firstDay is { } start)
                nodes.Add(new FilterCondition(name, ComparisonOperator.GreaterThanOrEqual, ToInstant(start)));
            if (lastDay is { } end)
                nodes.Add(new FilterCondition(name, ComparisonOperator.LessThan, ToInstant(end.AddDays(1))));
        }

        private static DateTime ToInstant(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
    }
}
