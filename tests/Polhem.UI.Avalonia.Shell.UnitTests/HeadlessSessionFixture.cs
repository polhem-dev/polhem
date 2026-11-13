using Avalonia;
using Avalonia.Headless;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// Starts the Avalonia headless platform for the <see cref="AvaloniaControlsCollection"/> tests. Every test of the
/// collection runs its body on the session's UI thread through <see cref="RunAsync(Func{Task})"/>, with a new
/// application and dispatcher for each test.
/// </summary>
public sealed class HeadlessSessionFixture : IDisposable
{
    private readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.StartNew(typeof(HeadlessSessionFixture), AvaloniaTestIsolationLevel.PerTest);

    /// <summary>
    /// Returns the application builder the session starts from: the themed test application on the headless
    /// platform, drawing nothing.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HeadlessTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });

    /// <summary>Runs <paramref name="action"/> on the UI thread.</summary>
    public Task RunAsync(Action action)
        => _session.Dispatch(action, CancellationToken.None);

    /// <summary>Runs <paramref name="action"/> on the UI thread and waits for it.</summary>
    public Task RunAsync(Func<Task> action)
        => _session.Dispatch(async () =>
        {
            await action();
            return true;
        }, CancellationToken.None);

    /// <inheritdoc/>
    public void Dispose() => _session.Dispose();
}
