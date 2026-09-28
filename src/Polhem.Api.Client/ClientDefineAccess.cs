using System.Collections.Concurrent;
using System.Text;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;

namespace Polhem.Api.Client
{
    /// <summary>
    /// Client-side, asynchronous, strongly-typed definition cache for retrieving and saving
    /// definition data through the API.
    /// </summary>
    /// <remarks>
    /// Exposes typed async accessors (<c>GetFormSchemaAsync</c>, <c>GetProgramSettingsAsync</c>, and
    /// friends) that retrieve definition data from the server, caching each result per instance so
    /// repeated reads of the same definition avoid a round-trip. Concurrent reads of the same key
    /// share a single in-flight request, and a failed request is evicted so the next read retries.
    /// The accessors are asynchronous end-to-end, so they are safe on single-threaded runtimes such
    /// as browser WASM. Call <see cref="ClearCache"/> after a tenant switch (<c>EnterCompany</c> /
    /// <c>LeaveCompany</c>) to drop the previous tenant's overlaid results.
    /// <para>
    /// A cancellation token passed to a <c>Get*Async</c> accessor cancels only that caller's wait.
    /// The fetch itself is shared by every concurrent caller of the same key, so it runs to
    /// completion and is cached; one caller giving up must not fail the others.
    /// </para>
    /// </remarks>
    public sealed class ClientDefineAccess
    {
        private readonly SystemApiConnector _connector;
        private readonly ConcurrentDictionary<string, Lazy<Task<object>>> _list;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientDefineAccess"/> class.
        /// </summary>
        /// <param name="connector">The system-level API service connector.</param>
        public ClientDefineAccess(SystemApiConnector connector)
        {
            _connector = connector;
            _list = new(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        /// <summary>
        /// Gets the system-level API service connector.
        /// </summary>
        private SystemApiConnector Connector
        {
            get { return _connector; }
        }

        /// <summary>
        /// Gets the cache of in-flight or completed definition fetches, keyed by define type and keys.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Caching the <see cref="Task{TResult}"/> rather than the result deduplicates concurrent
        /// misses on the same key: the second caller awaits the same in-flight fetch instead of
        /// issuing a second round-trip.
        /// </para>
        /// <para>
        /// WARNING: both halves of the type matter. The store is concurrent because callers arrive
        /// from the thread pool — the UI views kick their loads off without awaiting and continue on
        /// <c>ConfigureAwait(false)</c> continuations — so a plain dictionary would be read while
        /// another thread resizes it. The <see cref="Lazy{T}"/> is what actually delivers the
        /// single-in-flight guarantee: <c>GetOrAdd</c> may run its value factory more than once
        /// under contention and discard the losers, which for a factory that starts a request would
        /// mean the extra round-trips this cache exists to prevent.
        /// </para>
        /// </remarks>
        private ConcurrentDictionary<string, Lazy<Task<object>>> List
        {
            get { return _list; }
        }

        /// <summary>
        /// Gets the cache key for a definition object.
        /// </summary>
        /// <param name="defineType">The definition data type.</param>
        /// <param name="keys">The keys used to access the definition data.</param>
        private static string GetCacheKey(DefineType defineType, string[]? keys = null)
        {
            if (keys == null || keys.Length == 0)
                return defineType.ToString();

            var builder = new StringBuilder(defineType.ToString()).Append('_');
            foreach (string value in keys)
                builder.Append('.').Append(value);
            return builder.ToString();
        }

        /// <summary>
        /// Asynchronously gets definition data of the specified type, using the cache when available.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="defineType">The definition data type.</param>
        /// <param name="keys">The keys used to locate the definition data.</param>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        private async Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys, CancellationToken cancellationToken)
        {
            string cacheKey = GetCacheKey(defineType, keys);
            var entry = this.List.GetOrAdd(
                cacheKey,
                _ => new Lazy<Task<object>>(() => FetchAsync<T>(defineType, keys)));
            try
            {
                return (T)await entry.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Only this caller's wait was cancelled; the shared fetch is unaffected and stays cached.
                throw;
            }
            catch
            {
                // A failed fetch must not poison the cache. The compare-and-remove overload evicts
                // only when the faulted entry is still the cached one, so a concurrent retry that
                // already replaced it survives — and unlike a TryGetValue/TryRemove pair, nothing
                // can slip in between the check and the removal.
                this.List.TryRemove(new KeyValuePair<string, Lazy<Task<object>>>(cacheKey, entry));
                throw;
            }
        }

        /// <summary>
        /// Downloads definition data from the API and boxes it for the task cache.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="defineType">The definition data type.</param>
        /// <param name="keys">The keys used to locate the definition data.</param>
        private async Task<object> FetchAsync<T>(DefineType defineType, string[]? keys)
        {
            return (await this.Connector.GetDefineAsync<T>(defineType, keys).ConfigureAwait(false))!;
        }

        /// <summary>
        /// Asynchronously saves definition data via the API.
        /// </summary>
        /// <remarks>
        /// Every <c>Save*Async</c> method on this class routes through here, and the server-side
        /// <c>SystemBusinessObject.SaveDefine</c> (in <c>Polhem.Business</c>, which this assembly does not
        /// reference) is <c>LocalOnly</c>: writing a definition is a
        /// deployment-time operation. On a local connection these succeed; on a remote one the
        /// server rejects the call, which reaches the caller as a
        /// <see cref="Polhem.Base.Exceptions.UserMessageException"/>. Reading definitions works over
        /// both.
        /// </remarks>
        /// <param name="defineType">The definition data type.</param>
        /// <param name="defineObject">The definition data object.</param>
        /// <param name="keys">The keys used to locate where the definition data is saved.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        private Task<SaveDefineResponse> SaveDefineAsync(DefineType defineType, object defineObject, string[]? keys, CancellationToken cancellationToken)
        {
            return this.Connector.SaveDefineAsync(defineType, defineObject, keys, cancellationToken);
        }

        /// <summary>
        /// Clears the local definition cache.
        /// </summary>
        /// <remarks>
        /// Must be called after switching tenant context (<c>EnterCompany</c> / <c>LeaveCompany</c>
        /// changes the session's customization code). The server resolves the menu against the
        /// session's customization code, and serves the customization layers of FormLayout and
        /// Language for that code, but this cache keys them only by progId / layoutId / language and
        /// namespace — without a flush, a tenant switch would keep serving the previous tenant's
        /// result. The other definitions are tenant-agnostic, so clearing them too is merely a
        /// harmless re-fetch on next access. The UI client's <c>ClientInfo</c> calls this itself when
        /// it applies an <c>EnterCompany</c> result or clears the company context.
        /// </remarks>
        public void ClearCache()
        {
            this.List.Clear();
        }

        #region Get

        /// <summary>
        /// Asynchronously gets the system settings.
        /// </summary>
        /// <remarks>
        /// Server-side definition: this succeeds on a local connection (tooling) and is rejected on a
        /// remote one. The server serves remote callers only the definition types clients render from.
        /// </remarks>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<SystemSettings> GetSystemSettingsAsync(CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<SystemSettings>(DefineType.SystemSettings, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets the database settings.
        /// </summary>
        /// <remarks>
        /// Server-side definition: this succeeds on a local connection (tooling) and is rejected on a
        /// remote one. The server serves remote callers only the definition types clients render from.
        /// </remarks>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<DatabaseSettings> GetDatabaseSettingsAsync(CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<DatabaseSettings>(DefineType.DatabaseSettings, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets the program type registry.
        /// </summary>
        /// <remarks>
        /// Server-side definition: this succeeds on a local connection (tooling) and is rejected
        /// on a remote one. A shell wanting navigation calls <see cref="GetMenuSettingsAsync"/> instead.
        /// </remarks>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<ProgramSettings> GetProgramSettingsAsync(CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<ProgramSettings>(DefineType.ProgramSettings, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets the menu definition, already resolved against this session's tenant
        /// customization by the server.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<MenuSettings> GetMenuSettingsAsync(CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<MenuSettings>(DefineType.MenuSettings, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets the permission model registry.
        /// </summary>
        /// <remarks>
        /// Server-side definition: this succeeds on a local connection (tooling) and is rejected on a
        /// remote one. The server serves remote callers only the definition types clients render from.
        /// </remarks>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<PermissionModels> GetPermissionModelsAsync(CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<PermissionModels>(DefineType.PermissionModels, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets the database category settings.
        /// </summary>
        /// <remarks>
        /// Server-side definition: this succeeds on a local connection (tooling) and is rejected on a
        /// remote one. The server serves remote callers only the definition types clients render from.
        /// </remarks>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<DbCategorySettings> GetDbCategorySettingsAsync(CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<DbCategorySettings>(DefineType.DbCategorySettings, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets the table schema for the specified category and table.
        /// </summary>
        /// <param name="categoryId">The database category id.</param>
        /// <param name="tableName">The table name.</param>
        /// <remarks>
        /// Server-side definition: this succeeds on a local connection (tooling) and is rejected on a
        /// remote one. The server serves remote callers only the definition types clients render from.
        /// </remarks>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<TableSchema> GetTableSchemaAsync(string categoryId, string tableName, CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<TableSchema>(DefineType.TableSchema, new string[] { categoryId, tableName }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets the form schema for the specified program.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<FormSchema> GetFormSchemaAsync(string progId, CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<FormSchema>(DefineType.FormSchema, new string[] { progId }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets the form layout for the specified layout identifier.
        /// </summary>
        /// <param name="layoutId">The layout identifier.</param>
        /// <remarks>
        /// This is the base layer exactly as stored — no customization overlay, no generation. The
        /// tenant's layer comes from <see cref="GetCustomizeFormLayoutAsync"/> as a separate call,
        /// and picking between the two is the caller's job (see <see cref="Polhem.Api.Client.Definitions.FormDefinitionLoader"/>).
        /// <see cref="ClearCache"/> on tenant switch keeps the cache consistent.
        /// </remarks>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<FormLayout> GetFormLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<FormLayout>(DefineType.FormLayout, new string[] { layoutId }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets the tenant customization layer of a form layout definition;
        /// <c>null</c> when this session's tenant supplies no override.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="layoutId">The layout identifier; empty resolves to <paramref name="progId"/>.</param>
        /// <remarks>
        /// Cached alongside the base layer under a distinct key, so both layers survive one round
        /// trip each. <see cref="ClearCache"/> on tenant switch is what keeps the customization
        /// entries from outliving the tenant they belong to.
        /// </remarks>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<FormLayout?> GetCustomizeFormLayoutAsync(string progId, string layoutId = "",
            CancellationToken cancellationToken = default)
            => GetCustomizeAsync(
                $"Customize_{DefineType.FormLayout}_{progId}.{layoutId}",
                () => Connector.GetCustomizeFormLayoutAsync(progId, layoutId, CancellationToken.None),
                cancellationToken);

        /// <summary>
        /// Asynchronously gets the tenant customization layer of a language resource;
        /// <c>null</c> when this session's tenant supplies no override.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="ns">The resource namespace.</param>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<LanguageResource?> GetCustomizeLanguageAsync(string lang, string ns, CancellationToken cancellationToken = default)
            => GetCustomizeAsync(
                $"Customize_{DefineType.Language}_{lang}.{ns}",
                () => Connector.GetCustomizeLanguageAsync(lang, ns, CancellationToken.None),
                cancellationToken);

        /// <summary>
        /// Shares the definition task cache for customization fetches. "No override" is a normal
        /// answer, so a <c>null</c> result is cached too rather than re-fetched on every lookup;
        /// the boxed sentinel is needed because the cache stores <c>Task&lt;object&gt;</c>.
        /// </summary>
        private async Task<T?> GetCustomizeAsync<T>(string cacheKey, Func<Task<T?>> fetch, CancellationToken cancellationToken) where T : class
        {
            var entry = this.List.GetOrAdd(
                cacheKey,
                _ => new Lazy<Task<object>>(() => FetchCustomizeAsync(fetch)));
            try
            {
                return await entry.Value.WaitAsync(cancellationToken).ConfigureAwait(false) as T;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                this.List.TryRemove(new KeyValuePair<string, Lazy<Task<object>>>(cacheKey, entry));
                throw;
            }
        }

        private static async Task<object> FetchCustomizeAsync<T>(Func<Task<T?>> fetch) where T : class
            => (object?)await fetch().ConfigureAwait(false) ?? s_noCustomize;

        /// <summary>Marks "this tenant has no override" in the task cache, which cannot hold nulls.</summary>
        private static readonly object s_noCustomize = new();

        /// <summary>
        /// Asynchronously gets the language resource for the specified language and namespace.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="ns">The resource namespace.</param>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<LanguageResource> GetLanguageAsync(string lang, string ns, CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<LanguageResource>(DefineType.Language, new string[] { lang, ns }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets the system currency master (per-currency decimal places). Returns
        /// <c>null</c> when no currency master is deployed, in which case amounts fall back to
        /// framework-default decimal places.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<CurrencySettings> GetCurrencySettingsAsync(CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<CurrencySettings>(DefineType.CurrencySettings, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets the system unit-of-measure master (per-unit decimal places). Returns
        /// <c>null</c> when no unit master is deployed, in which case quantities/weights fall back to
        /// framework-default decimal places.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels this caller's wait.</param>
        public Task<UnitSettings> GetUnitSettingsAsync(CancellationToken cancellationToken = default)
        {
            return GetDefineAsync<UnitSettings>(DefineType.UnitSettings, null, cancellationToken);
        }

        #endregion

        #region Save

        /// <summary>
        /// Asynchronously saves the system settings.
        /// </summary>
        /// <param name="settings">The system settings.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public Task SaveSystemSettingsAsync(SystemSettings settings, CancellationToken cancellationToken = default)
        {
            return SaveDefineAsync(DefineType.SystemSettings, settings, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously saves the database settings.
        /// </summary>
        /// <param name="settings">The database settings.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public Task SaveDatabaseSettingsAsync(DatabaseSettings settings, CancellationToken cancellationToken = default)
        {
            return SaveDefineAsync(DefineType.DatabaseSettings, settings, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously saves the program settings.
        /// </summary>
        /// <param name="settings">The program settings.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public Task SaveProgramSettingsAsync(ProgramSettings settings, CancellationToken cancellationToken = default)
        {
            return SaveDefineAsync(DefineType.ProgramSettings, settings, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously saves the permission model registry.
        /// </summary>
        /// <param name="models">The permission model registry.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public Task SavePermissionModelsAsync(PermissionModels models, CancellationToken cancellationToken = default)
        {
            return SaveDefineAsync(DefineType.PermissionModels, models, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously saves the database category settings.
        /// </summary>
        /// <param name="settings">The database category settings.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public Task SaveDbCategorySettingsAsync(DbCategorySettings settings, CancellationToken cancellationToken = default)
        {
            return SaveDefineAsync(DefineType.DbCategorySettings, settings, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously saves the table schema.
        /// </summary>
        /// <param name="categoryId">The database category id.</param>
        /// <param name="tableSchema">The table schema.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public Task SaveTableSchemaAsync(string categoryId, TableSchema tableSchema, CancellationToken cancellationToken = default)
        {
            return SaveDefineAsync(DefineType.TableSchema, tableSchema, new string[] { categoryId }, cancellationToken);
        }

        /// <summary>
        /// Asynchronously saves the form schema.
        /// </summary>
        /// <param name="formSchema">The form schema.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public Task SaveFormSchemaAsync(FormSchema formSchema, CancellationToken cancellationToken = default)
        {
            return SaveDefineAsync(DefineType.FormSchema, formSchema, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously saves the form layout.
        /// </summary>
        /// <param name="formLayout">The form layout.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public Task SaveFormLayoutAsync(FormLayout formLayout, CancellationToken cancellationToken = default)
        {
            return SaveDefineAsync(DefineType.FormLayout, formLayout, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously saves the language resource.
        /// </summary>
        /// <param name="resource">The language resource.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public Task SaveLanguageAsync(LanguageResource resource, CancellationToken cancellationToken = default)
        {
            return SaveDefineAsync(DefineType.Language, resource, new string[] { resource.Lang, resource.Namespace }, cancellationToken);
        }

        #endregion
    }
}
