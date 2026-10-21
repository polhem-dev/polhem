using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Polhem.UI.ApiBridge;
using Polhem.UI.Avalonia.Shell.Localization;

namespace Polhem.UI.Avalonia.Shell.ViewModels
{
    /// <summary>
    /// First step: the endpoint and the API key. Connecting pings the endpoint through
    /// <see cref="ClientInfo.InitializeAsync(string, System.Threading.CancellationToken)"/>, and on success the shell
    /// moves on to sign-in.
    /// </summary>
    internal sealed partial class ConnectionViewModel : ViewModelBase
    {
        private readonly ShellOptions _options;
        private readonly Action _onConnected;

        /// <summary>Gets or sets the JSON-RPC endpoint: the stored one, else the application's default.</summary>
        [ObservableProperty]
        private string _endpoint;

        /// <summary>
        /// Gets or sets the API key sent as <c>X-Api-Key</c>. Editing it persists the new value, so pointing the
        /// application at a backend that issued its own key needs no rebuild.
        /// </summary>
        [ObservableProperty]
        private string _apiKey = ClientInfo.GetApiKey();

        /// <summary>Gets the text under the title.</summary>
        [ObservableProperty]
        private string _description = string.Empty;

        /// <summary>Gets whether a connection attempt is in flight.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="options">The application's options.</param>
        /// <param name="onConnected">Invoked after a successful connection.</param>
        public ConnectionViewModel(ShellOptions options, Action onConnected)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(onConnected);
            _options = options;
            _onConnected = onConnected;

            var stored = ClientInfo.GetEndpoint();
            _endpoint = string.IsNullOrWhiteSpace(stored) ? options.DefaultEndpoint : stored;
            RefreshText();
            SetStatus("Status_Idle", isError: false);
        }

        /// <summary>Gets the application title.</summary>
        public string AppTitle => _options.AppTitle;

        /// <summary>Gets the inverse of <see cref="IsBusy"/>, bound to the inputs' <c>IsEnabled</c>.</summary>
        public bool IsNotBusy => !IsBusy;

        /// <inheritdoc/>
        public override void RefreshText()
        {
            base.RefreshText();
            Description = string.IsNullOrWhiteSpace(_options.Description)
                ? ShellLocalizer.Current["Connection_Hint"]
                : _options.Description;
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (IsBusy) return;

            var endpoint = Endpoint?.Trim() ?? string.Empty;
            if (endpoint.Length == 0)
            {
                SetStatus("Status_EndpointEmpty", isError: true);
                return;
            }

            IsBusy = true;
            SetStatus("Status_Pinging", isError: false, endpoint);
            try
            {
                // The key goes first: the configuration call that connecting makes carries it, so a wrong key shows
                // up here rather than on the first real call. The connection itself is fully async, which the
                // single-threaded browser runtime requires ("Cannot wait on monitors").
                ClientInfo.SetApiKey(ApiKey?.Trim() ?? string.Empty);
                await ClientInfo.InitializeAsync(endpoint).ConfigureAwait(true);

                SetStatus("Status_Connected", isError: false, endpoint);
                _onConnected();
            }
            catch (Exception ex)
            {
                // NOTE: any failure, of any type, is shown to the viewer; the screen is the place to report it, and
                // rethrowing from a command would end as an unobserved task exception.
                SetStatus("Status_ConnectFailed", isError: true, ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
