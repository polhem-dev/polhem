using System.Globalization;
using Microsoft.AspNetCore.Components;
using Polhem.Definition.Language;
using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// The state of the saved queries at the top of the query pane, which every UI package renders with its own markup:
    /// a picker that applies one, a marker when the applied one was changed, and a menu to save, save as, rename, share,
    /// delete and make one the default.
    /// </summary>
    /// <remarks>
    /// The bar only asks: the host holds the queries, calls the backend and asks for a name or a confirmation. Save,
    /// Rename, Share and Delete are offered only for a query the user created, as the backend refuses the rest; Save as
    /// is always offered, and the default can be set for any query that is applied. The Avalonia <c>UserQueryBar</c>
    /// follows the same rules.
    /// </remarks>
    public abstract class UserQueryBarBase : ComponentBase
    {
        /// <summary>Gets or sets the queries the user can apply, their own first.</summary>
        [Parameter, EditorRequired]
        public IReadOnlyList<UserQueryEntry> Entries { get; set; } = [];

        /// <summary>Gets or sets the applied query, or <c>null</c>.</summary>
        [Parameter]
        public UserQueryEntry? Current { get; set; }

        /// <summary>Gets or sets the <c>sys_rowid</c> of the user's default query, or <see cref="Guid.Empty"/>.</summary>
        [Parameter]
        public Guid DefaultRowId { get; set; }

        /// <summary>Gets or sets whether the conditions differ from the applied query as it was applied.</summary>
        [Parameter]
        public bool IsModified { get; set; }

        /// <summary>Gets or sets whether the bar waits, because a call is running.</summary>
        [Parameter]
        public bool Disabled { get; set; }

        /// <summary>Invoked with the query the user picks.</summary>
        [Parameter]
        public EventCallback<UserQueryEntry> Applied { get; set; }

        /// <summary>Invoked when the user saves the applied query with the conditions on screen.</summary>
        [Parameter]
        public EventCallback SaveRequested { get; set; }

        /// <summary>Invoked when the user asks to save the conditions under a new name.</summary>
        [Parameter]
        public EventCallback SaveAsRequested { get; set; }

        /// <summary>Invoked when the user asks to rename the applied query.</summary>
        [Parameter]
        public EventCallback RenameRequested { get; set; }

        /// <summary>Invoked when the user shares the applied query, or stops sharing it.</summary>
        [Parameter]
        public EventCallback ShareToggled { get; set; }

        /// <summary>Invoked when the user makes the applied query the default, or stops it being so.</summary>
        [Parameter]
        public EventCallback DefaultToggled { get; set; }

        /// <summary>Invoked when the user asks to delete the applied query.</summary>
        [Parameter]
        public EventCallback DeleteRequested { get; set; }

        [Inject]
        private IServiceProvider? Services { get; set; }

        /// <summary>Gets the <c>sys_rowid</c> of the applied query, or <see cref="Guid.Empty"/> for none.</summary>
        internal Guid CurrentRowId => Current?.RowId ?? Guid.Empty;

        /// <summary>Gets a value indicating whether the applied query can be changed: the user created it.</summary>
        internal bool CanChange => Current is { IsOwn: true };

        /// <summary>Gets a value indicating whether the applied query can be made the default or stop being it.</summary>
        internal bool CanToggleDefault => Current is not null;

        /// <summary>Gets the localized caption of the share action for the applied query.</summary>
        internal string ShareText => Text(Current is { IsShared: true } ? PolhemUIText.UserQueryUnshare : PolhemUIText.UserQueryShare);

        /// <summary>Gets the localized caption of the default action for the applied query.</summary>
        internal string DefaultText => Text(Current is not null && Current.RowId == DefaultRowId
            ? PolhemUIText.UserQueryClearDefault
            : PolhemUIText.UserQuerySetDefault);

        /// <summary>Returns a framework text in the user's language.</summary>
        /// <param name="key">The text's key in <see cref="PolhemUIText"/>.</param>
        /// <returns>The text.</returns>
        internal string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        /// <summary>Returns the text a query is listed under, marked when it is shared or the default.</summary>
        /// <param name="rowId">The query's <c>sys_rowid</c>; <see cref="Guid.Empty"/> names no query.</param>
        /// <returns>The text.</returns>
        internal string EntryText(Guid rowId)
        {
            var entry = Entries.FirstOrDefault(e => e.RowId == rowId);
            if (entry is null) return rowId == Guid.Empty ? Text(PolhemUIText.UserQueryNone) : string.Empty;
            var markers = new List<string>(2);
            if (entry.IsShared) markers.Add(Text(PolhemUIText.UserQueryShared));
            if (entry.RowId == DefaultRowId) markers.Add(Text(PolhemUIText.UserQueryDefault));
            return markers.Count == 0
                ? entry.Name
                : string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.UserQueryWithMarkers), entry.Name,
                    string.Join(Text(PolhemUIText.CriteriaValueSeparator), markers));
        }

        /// <summary>Applies the query the user picked.</summary>
        /// <param name="rowId">The picked query's <c>sys_rowid</c>.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it is sent.</param>
        /// <returns>The operation.</returns>
        internal Task PickAsync(Guid rowId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = Entries.FirstOrDefault(e => e.RowId == rowId);
            return entry is null || Disabled ? Task.CompletedTask : Applied.InvokeAsync(entry);
        }
    }
}
