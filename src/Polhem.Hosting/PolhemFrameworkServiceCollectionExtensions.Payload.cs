using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polhem.Api.Core.Transformers;
using Polhem.Definition.Settings;
using Polhem.JsonRpc.Payload;
using Polhem.JsonRpc.Payload.Server;

namespace Polhem.Hosting
{
    /// <summary>
    /// The payload settings of the server: the compressor and encryptor, the codecs, and replay protection.
    /// </summary>
    public static partial class PolhemFrameworkServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the payload options the server reads and writes the payload envelope with, built from the
        /// compressor and encryptor names of <paramref name="settings"/>, replacing any registered before.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="settings">The compressor and encryptor names, usually <c>CommonConfiguration.ApiPayloadOptions</c>.</param>
        /// <param name="isDebugMode">Whether the encryptor <c>none</c> is allowed.</param>
        /// <param name="configure">Further changes, for example <c>options.RequireFrame = true</c>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Without this call the framework uses gzip and aes-cbc-hmac with no frame. Clients must use the same compressor,
        /// encryptor and frame setting; a .NET client keeps them in <c>ApiClientInfo.PayloadOptions</c>. Sequence numbers
        /// are remembered in memory; register another <see cref="IPayloadReplayStore"/> when several server instances
        /// share sessions.
        /// </remarks>
        public static IServiceCollection AddPolhemPayload(this IServiceCollection services, ApiPayloadOptions settings,
            bool isDebugMode, Action<PayloadOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(settings);

            var options = PolhemPayload.CreateOptions(settings, isDebugMode);
            configure?.Invoke(options);
            services.RemoveAll<PayloadOptions>();
            services.AddSingleton(options);
            return services;
        }

        private static void AddPayloadDefaults(IServiceCollection services)
        {
            services.TryAddSingleton(_ => PolhemPayload.CreateOptions());
            // A remembered scope must outlive any frame its timestamp still admits, or a replay inside the window
            // would find the scope forgotten and start over.
            services.TryAddSingleton<IPayloadReplayStore>(sp =>
                new MemoryPayloadReplayStore(sp.GetRequiredService<PayloadOptions>().FrameTimestampTolerance * 2));
        }
    }
}
