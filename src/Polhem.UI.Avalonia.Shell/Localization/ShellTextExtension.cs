using Avalonia;
using Avalonia.Markup.Xaml;

namespace Polhem.UI.Avalonia.Shell.Localization
{
    /// <summary>
    /// XAML markup extension that binds a property to a text of <see cref="ShellLocalizer"/>:
    /// <c>{l:ShellText Login_Title}</c>.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: the value is an observable turned into a binding, not an indexer binding with a property change
    /// notification. Avalonia does not refresh a binding mounted inside a data template on an indexer notification,
    /// so a language switch would leave those texts unchanged; an observable binding is pushed every value.
    /// </remarks>
    internal sealed class ShellTextExtension : MarkupExtension
    {
        /// <summary>Initializes a new instance with no key.</summary>
        public ShellTextExtension() { }

        /// <summary>Initializes a new instance for <paramref name="key"/>.</summary>
        public ShellTextExtension(string key)
        {
            Key = key;
        }

        /// <summary>Gets or sets the resource key.</summary>
        public string Key { get; set; } = string.Empty;

        /// <inheritdoc/>
        public override object ProvideValue(IServiceProvider serviceProvider)
            => new TextObservable(ShellLocalizer.Current, Key).ToBinding();

        private sealed class TextObservable(ShellLocalizer localizer, string key) : IObservable<string>
        {
            public IDisposable Subscribe(IObserver<string> observer)
            {
                void Publish(object? sender, EventArgs e) => observer.OnNext(localizer[key]);

                Publish(null, EventArgs.Empty);
                localizer.CultureChanged += Publish;
                return new Subscription(() => localizer.CultureChanged -= Publish);
            }
        }

        private sealed class Subscription(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }
}
