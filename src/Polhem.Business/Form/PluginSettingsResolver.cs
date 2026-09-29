using System.Collections.Concurrent;
using Polhem.Core;
using Polhem.Definition.Customization;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Default <see cref="IFormPluginResolver"/>: reads the plugin chain from
    /// <see cref="PluginSettings"/>, base layer plus optional tenant customization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every failure throws</b>, as on the business-object and repository axes: a declared name
    /// that will not load is a configuration error, not something to work around. A plugin is
    /// something the author added on purpose, and skipping it would run the pipeline as though the
    /// customization were not there. Silently omitting a credit check is worse than refusing to save.
    /// </para>
    /// <para>
    /// Chains are cached by <c>(customizationCode, progId)</c>, which is also what makes the
    /// declaration-versus-override check a one-off. Each chain remembers the base and customization
    /// <see cref="PluginSettings"/> instances it was built from and is used only while the settings
    /// just read are those same instances, so a file-watcher reload, which hands back new instances,
    /// makes every chain build again on its next use.
    /// </para>
    /// </remarks>
    public sealed class PluginSettingsResolver : IFormPluginResolver
    {
        private readonly IDefineAccess _defineAccess;
        private readonly ICustomizeDefineReader? _customizeReader;
        private readonly ConcurrentDictionary<string, BuiltChain> _chainCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A cached chain and the settings instances it was built from.
        /// </summary>
        /// <remarks>
        /// NOTE: the settings references are the validity check, not a reset triggered by them. A
        /// reset left a window in which a build from the old settings could be added after the cache
        /// was cleared for the new ones, and it would then stay until the next reload.
        /// </remarks>
        private sealed record BuiltChain(PluginSettings? BaseSettings, PluginSettings? CustomizeSettings, FormPluginChain Chain);

        /// <summary>
        /// Initializes a new <see cref="PluginSettingsResolver"/> without customization support.
        /// </summary>
        /// <param name="defineAccess">The define access used to load <see cref="PluginSettings"/>.</param>
        public PluginSettingsResolver(IDefineAccess defineAccess) : this(defineAccess, null)
        {
        }

        /// <summary>
        /// Initializes a new <see cref="PluginSettingsResolver"/>.
        /// </summary>
        /// <param name="defineAccess">The define access used to load <see cref="PluginSettings"/>.</param>
        /// <param name="customizeReader">The customization-override reader; <c>null</c> disables the overlay.</param>
        public PluginSettingsResolver(IDefineAccess defineAccess, ICustomizeDefineReader? customizeReader)
        {
            _defineAccess = defineAccess ?? throw new ArgumentNullException(nameof(defineAccess));
            _customizeReader = customizeReader;
        }

        /// <summary>
        /// Runs after a chain is built and before it is cached. Exposed for tests, which land a
        /// settings reload inside that window.
        /// </summary>
        internal Action? BeforeCacheWrite { get; set; }

        /// <inheritdoc/>
        public FormPluginChain Resolve(string customizeId, string progId)
        {
            ArgumentException.ThrowIfNullOrEmpty(progId);

            PluginSettings? baseSettings;
            try
            {
                baseSettings = _defineAccess.GetPluginSettings();
            }
            catch (FileNotFoundException)
            {
                // No base definition is the normal state — most deployments bind no plugins at all.
                // A customization may still declare some, so resolution continues.
                baseSettings = null;
            }

            PluginSettings? custSettings = null;
            if (!string.IsNullOrEmpty(customizeId) && _customizeReader is not null)
                custSettings = _customizeReader.GetCustomizePluginSettings(customizeId);

            // The NUL separator cannot occur in either part, so distinct pairs never collide; the
            // empty-code key is just the progId, keeping the base path identical to a host with no
            // customization configured.
            string cacheKey = string.IsNullOrEmpty(customizeId)
                ? progId
                : customizeId + "\0" + progId;

            if (_chainCache.TryGetValue(cacheKey, out var cached)
                && ReferenceEquals(cached.BaseSettings, baseSettings)
                && ReferenceEquals(cached.CustomizeSettings, custSettings))
                return cached.Chain;

            var chain = BuildChain(custSettings, baseSettings, progId);
            BeforeCacheWrite?.Invoke();
            _chainCache[cacheKey] = new BuiltChain(baseSettings, custSettings, chain);
            return chain;
        }

        private static FormPluginChain BuildChain(PluginSettings? custSettings, PluginSettings? baseSettings, string progId)
        {
            // How the two layers combine is decided by CustomizeOverlay — plugins concatenate,
            // base first, which is the one granularity there that adds rather than chooses.
            var declared = CustomizeOverlay.GetPluginBindings(custSettings, baseSettings, progId);
            if (declared.Count == 0) { return FormPluginChain.Empty; }

            // The declared stage travels with the type all the way into the chain, which then
            // refuses the whole chain if the class and the file disagree. This gate is the one that
            // covers hand-authored files: the packaged layer has no maintenance API at all.
            var resolved = new List<FormPluginBinding>(declared.Count);
            foreach (var binding in declared)
                resolved.Add(new FormPluginBinding(LoadPluginType(progId, binding.Type), binding.Stage));
            return FormPluginChain.Create(progId, resolved);
        }

        private static Type LoadPluginType(string progId, string typeName)
        {
            Type? type;
            try
            {
                // `AssemblyLoader.LoadAssembly` throws when the assembly cannot be located.
                // When it loads but the type is absent, `AssemblyLoader.GetType` returns null
                // instead of throwing, so both outcomes have to be handled here.
                type = AssemblyLoader.GetType(typeName);
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                throw Unloadable(progId, typeName, ex);
            }

            if (type == null)
                throw Unloadable(progId, typeName, inner: null);

            if (!typeof(FormBusinessPlugin).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(
                    $"PluginSettings binds progId '{progId}' to plugin '{typeName}', which does not derive from " +
                    $"{typeof(FormBusinessPlugin).FullName}.");
            }

            return type;
        }

        private static InvalidOperationException Unloadable(string progId, string typeName, Exception? inner)
            => new(
                $"PluginSettings binds progId '{progId}' to plugin '{typeName}', which cannot be loaded. " +
                "Fix the assembly-qualified type name, or remove the binding.",
                inner);
    }
}
