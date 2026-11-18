using Avalonia.Controls;
using Avalonia.DemoCenter.Modules.WithoutBackend;
using Polhem.UI.Avalonia.Views;

namespace Avalonia.DemoCenter.Modules.QueryConditions
{
    /// <summary>
    /// The query pane <see cref="ListView"/> builds itself: it offers the schema's
    /// <see cref="Polhem.Definition.Forms.FormSchema.QueryFields"/> behind its Query button, shows the conditions as chips
    /// above the list, and sends them as the filter of <c>GetListAsync</c>, which the in-memory connector applies.
    /// </summary>
    public sealed class ListQueryModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Query Conditions";

        /// <inheritdoc/>
        public override string Title => "ListView query pane";

        /// <inheritdoc/>
        public override string Description =>
            "Press Query to open the condition pane beside the list (or over it on a narrow window). The conditions show "
            + "as chips above the list; a chip removes its condition, and the list reloads through the connector's "
            + "GetListAsync with the filter applied.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var schema = OrderQueryData.BuildSchema();
            var connector = new InMemoryFormApiConnector(schema, OrderQueryData.SeedRows(schema));
            var list = new ListView { Schema = schema, FormConnector = connector };
            return list;
        }
    }
}
