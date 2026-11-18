using Avalonia.Controls;
using Polhem.UI.Avalonia.Views;
using Polhem.UI.Core.Criteria;

namespace Avalonia.DemoCenter.Modules.QueryConditions
{
    /// <summary>
    /// Saved queries: when the list's connector also implements <see cref="Polhem.Api.Contracts.Form.IUserQueryConnector"/>,
    /// <see cref="ListView"/> shows a picker at the top of its query pane and applies the user's default when it opens.
    /// The criteria are stored as the text <see cref="UserQueryCodec"/> writes.
    /// </summary>
    public sealed class SavedQueriesModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Query Conditions";

        /// <inheritdoc/>
        public override string Title => "Saved queries";

        /// <inheritdoc/>
        public override string Description =>
            "The list opens with the default query \"Open orders this month\" applied. Open the Query pane to pick another "
            + "one, or change a condition (a * marks the change) and use the ⋯ menu to save, save as, rename, share, delete "
            + "or make a query the default. \"Large shipped orders\" is shared by another user: it can be applied and "
            + "saved under a new name, not changed.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var schema = OrderQueryData.BuildSchema();
            var fields = CriteriaField.FromQueryFields(schema);
            var connector = new InMemoryUserQueryConnector(schema, OrderQueryData.SeedRows(schema));

            connector.Seed("Open orders this month", OpenThisMonth(fields), isShared: false, isOwn: true, isDefault: true);
            connector.Seed("Unpaid orders", Unpaid(fields), isShared: true, isOwn: true);
            connector.Seed("Large shipped orders", LargeShipped(fields), isShared: true, isOwn: false);

            var list = new ListView { Schema = schema, FormConnector = connector };
            return list;
        }

        private static string OpenThisMonth(IReadOnlyList<CriteriaField> fields)
        {
            var criteria = new QueryCriteria();
            AddStatus(criteria, fields, "OPEN", "Open");
            criteria.Add(Field(fields, "order_date"), CriteriaOperator.Range).SetRelativeDate(new RelativeDate(RelativeDateKind.ThisMonth));
            return UserQueryCodec.Serialize(criteria);
        }

        private static string Unpaid(IReadOnlyList<CriteriaField> fields)
        {
            var criteria = new QueryCriteria();
            criteria.Add(Field(fields, "is_paid")).SetValue(false);
            return UserQueryCodec.Serialize(criteria);
        }

        private static string LargeShipped(IReadOnlyList<CriteriaField> fields)
        {
            var criteria = new QueryCriteria();
            AddStatus(criteria, fields, "SHIPPED", "Shipped");
            criteria.Add(Field(fields, "amount"), CriteriaOperator.Range).SetRange(900m, null);
            return UserQueryCodec.Serialize(criteria);
        }

        private static void AddStatus(QueryCriteria criteria, IReadOnlyList<CriteriaField> fields, string value, string text)
            => criteria.Add(Field(fields, "status")).TryAddValue(value, text);

        private static CriteriaField Field(IReadOnlyList<CriteriaField> fields, string name)
            => fields.Single(f => f.FieldName == name);
    }
}
