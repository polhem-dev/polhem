using Microsoft.Playwright;

namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// Counts what the circuit sends a page, to measure the traffic of one action.
    /// </summary>
    /// <remarks>
    /// The circuit's frames are what Blazor Server sends on each render, so their size follows the number of attribute and
    /// element edits a render produced. Playwright's network idle does not see them, so an action counts as finished once
    /// no frame has arrived for a quiet period.
    /// </remarks>
    public sealed class CircuitTrafficMeter
    {
        private static readonly TimeSpan s_quietPeriod = TimeSpan.FromMilliseconds(700);
        private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(20);
        private readonly IPage _page;
        private long _bytes;
        private long _frames;

        /// <summary>
        /// Starts counting the frames a page receives. Create it before the page opens its circuit.
        /// </summary>
        /// <param name="page">The page.</param>
        public CircuitTrafficMeter(IPage page)
        {
            _page = page;
            page.WebSocket += (_, socket) => socket.FrameReceived += (_, frame) =>
            {
                Interlocked.Add(ref _bytes, frame.Binary?.Length ?? frame.Text?.Length ?? 0);
                Interlocked.Increment(ref _frames);
            };
        }

        /// <summary>
        /// Waits until the circuit is quiet, runs an action and returns how many bytes the circuit sent for it.
        /// </summary>
        /// <param name="action">The action, such as a click or a key press.</param>
        /// <returns>The bytes received from the action until the circuit was quiet again.</returns>
        public async Task<long> MeasureAsync(Func<Task> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            await WaitForQuietAsync().ConfigureAwait(false);
            long before = Interlocked.Read(ref _bytes);
            await action().ConfigureAwait(false);
            await WaitForQuietAsync().ConfigureAwait(false);
            return Interlocked.Read(ref _bytes) - before;
        }

        private async Task WaitForQuietAsync()
        {
            var deadline = DateTime.UtcNow + s_deadline;
            long last = Interlocked.Read(ref _frames);
            var quietSince = DateTime.UtcNow;
            while (DateTime.UtcNow - quietSince < s_quietPeriod)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException($"The circuit was still sending after {s_deadline.TotalSeconds} seconds.");
                await _page.WaitForTimeoutAsync(50).ConfigureAwait(false);
                long now = Interlocked.Read(ref _frames);
                if (now != last)
                {
                    last = now;
                    quietSince = DateTime.UtcNow;
                }
            }
        }
    }
}
