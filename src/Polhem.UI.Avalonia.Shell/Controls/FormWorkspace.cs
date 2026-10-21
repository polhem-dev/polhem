using Avalonia.Controls;
using Polhem.UI.Avalonia.Views;

namespace Polhem.UI.Avalonia.Shell.Controls
{
    /// <summary>
    /// One form's workspace: hosts the <see cref="ListView"/> browse surface and swaps in a <see cref="FormView"/>
    /// for the selected record, switching back to the list when the record is saved or dismissed.
    /// </summary>
    /// <remarks>
    /// The framework ships <see cref="ListView"/> and <see cref="FormView"/> as independent controls and leaves the
    /// switching to the host; this is that host-side orchestration of the list ↔ record flow. Both surfaces localize
    /// through the client-wide definition loader (<see cref="Polhem.UI.ApiBridge.ClientInfo.UseDefinitionLoader"/>).
    /// </remarks>
    internal sealed class FormWorkspace : UserControl
    {
        private readonly string _progId;
        private readonly ListView _list;
        private readonly ContentControl _host;

        /// <summary>
        /// Initializes a new workspace for <paramref name="progId"/>, starting on the list.
        /// </summary>
        public FormWorkspace(string progId)
        {
            _progId = progId;

            _list = new ListView { ProgId = progId };
            _list.ViewRequested += (_, id) => ShowRecord(record => record.ViewAsync(id));
            _list.EditRequested += (_, id) => ShowRecord(record => record.EditAsync(id));
            _list.AddRequested += (_, _) => ShowRecord(record => record.NewAsync());

            _host = new ContentControl { Content = _list };
            Content = _host;
        }

        /// <summary>
        /// Unwinds one navigation level for a platform back request: an open record returns to the list, mirroring
        /// the on-screen Back button.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when a record was open and the workspace returned to the list;
        /// <see langword="false"/> when the list was already showing.
        /// </returns>
        public bool TryGoBack()
        {
            if (_host.Content is FormView)
            {
                ReturnToList(reload: false);
                return true;
            }

            return false;
        }

        private void ShowRecord(Func<FormView, Task> start)
        {
            var record = new FormView { ProgId = _progId };
            record.Saved += (_, _) => ReturnToList(reload: true);
            record.Closed += (_, _) => ReturnToList(reload: false);

            _host.Content = record;
            _ = start(record);
        }

        private void ReturnToList(bool reload)
        {
            _host.Content = _list;
            if (reload)
                _ = _list.ReloadAsync();
        }
    }
}
