using Polhem.Base.Serialization;
using Polhem.Definition.Customization;
using Polhem.Definition.Settings;
using Microsoft.Extensions.Logging;

namespace Polhem.ObjectCaching
{
    /// <summary>
    /// Typed accessors for the settings family (system, database, program, menu, plugin, permission, category, currency, unit).
    /// </summary>
    public partial class CacheDefineAccess
    {
        /// <summary>
        /// Gets the system settings.
        /// </summary>
        public SystemSettings GetSystemSettings()
        {
            return _cache.SystemSettings.Get()!;
        }

        /// <summary>
        /// Saves the system settings.
        /// </summary>
        /// <param name="settings">The system settings.</param>
        public void SaveSystemSettings(SystemSettings settings)
        {
            // Save system settings to file
            string filePath = _paths.GetSystemSettingsFilePath();
            XmlCodec.SerializeToFile(settings, filePath);
            // Invalidate the cache
            _cache.SystemSettings.Remove();
        }

        /// <summary>
        /// Gets the database settings. <see cref="DatabaseServer.Password"/> and
        /// <see cref="DatabaseItem.Password"/> are decrypted in place on first read
        /// (subsequent cache hits see plain text and the decrypt step is an idempotent no-op).
        /// </summary>
        /// <remarks>
        /// WARNING: this mutates a cached instance, which the framework otherwise forbids — see
        /// "Cached Data Immutability After Init" in the development constraints. It is admitted here
        /// rather than hidden because the reasoning does not generalise, and a reader who copies the
        /// shape without the reasoning will land somewhere it does not hold.
        /// <para>
        /// Why it converges: <c>Decrypt</c> returns a value with no <c>enc:</c> prefix unchanged, so
        /// the operation is idempotent; each caller runs the whole decrypt from the top, so a caller
        /// that races a partially-decrypted instance finishes the job itself; and a string
        /// assignment is atomic, so no reader ever observes a half-written value. Every one of those
        /// three has to hold. Mutating a cached instance in any way that is <b>not</b> idempotent,
        /// or that writes anything wider than a reference, is a cross-session data leak.
        /// </para>
        /// </remarks>
        public DatabaseSettings GetDatabaseSettings()
        {
            var settings = _cache.DatabaseSettings.Get()!;
            // Reported once per instance: this runs on every connection, and the first read happens as
            // the host starts talking to its databases, which is when an operator is looking.
            if (_configEncryptionKey.Length == 0 && Interlocked.Exchange(ref _unprotectedPasswordsReported, 1) == 0)
            {
                WarnIfPasswordsUnprotected(settings, "loaded");
            }
            DatabaseSettingsCryptor.DecryptInPlace(settings, _configEncryptionKey);
            return settings;
        }

        /// <summary>
        /// Saves the database settings. Plain-text <see cref="DatabaseServer.Password"/> /
        /// <see cref="DatabaseItem.Password"/> are encrypted in the file that is written
        /// (already-prefixed <c>enc:</c> values pass through); <paramref name="settings"/> itself is
        /// left as it was, apart from its bound file path.
        /// </summary>
        /// <param name="settings">The database settings.</param>
        /// <remarks>
        /// Without a configuration encryption key the passwords are written as they are, and a warning
        /// is logged each time that happens.
        /// <para>
        /// The encryption runs on a copy. The natural edit pattern is Get, change, Save, and what Get
        /// returns is the cached instance every connection reads: encrypting it in place would hand
        /// concurrent readers ciphertext as a connection password until a later Get decrypted it again.
        /// </para>
        /// </remarks>
        public void SaveDatabaseSettings(DatabaseSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (_configEncryptionKey.Length == 0)
            {
                WarnIfPasswordsUnprotected(settings, "saved");
            }
            var toWrite = settings.Clone();
            DatabaseSettingsCryptor.EncryptInPlace(toWrite, _configEncryptionKey);
            string filePath = _paths.GetDatabaseSettingsFilePath();
            XmlCodec.SerializeToFile(toWrite, filePath);
            settings.SetObjectFilePath(filePath);
            // Invalidate the cache
            _cache.DatabaseSettings.Remove();
        }

        /// <summary>
        /// Logs a warning when <paramref name="settings"/> holds passwords but no configuration
        /// encryption key is configured, so they are neither encrypted on save nor decrypted on read.
        /// </summary>
        /// <param name="settings">The database settings being loaded or saved.</param>
        /// <param name="operation">"loaded" or "saved", for the message.</param>
        private void WarnIfPasswordsUnprotected(DatabaseSettings settings, string operation)
        {
            if (_logger == null || !DatabaseSettingsCryptor.HasPasswords(settings)) { return; }

            _logger.LogWarning(
                "DatabaseSettings was {Operation} with database passwords, but no ConfigEncryptionKey is configured " +
                "in SystemSettings (BackendConfiguration/SecurityKeySettings). Plain passwords are stored in clear " +
                "text and 'enc:' values cannot be decrypted. Configure a ConfigEncryptionKey to protect them.",
                operation);
        }

        /// <summary>
        /// Gets the program settings.
        /// </summary>
        public ProgramSettings GetProgramSettings()
        {
            return _cache.ProgramSettings.Get()!;
        }

        /// <summary>
        /// Saves the program settings.
        /// </summary>
        /// <param name="settings">The program settings.</param>
        public void SaveProgramSettings(ProgramSettings settings)
        {
            // Save program settings through the active storage, then invalidate the cache.
            _storage.SaveProgramSettings(settings);
            _cache.ProgramSettings.Remove();
        }

        /// <summary>
        /// Gets the base-layer menu definition.
        /// </summary>
        public MenuSettings GetMenuSettings()
        {
            return _cache.MenuSettings.Get()!;
        }

        /// <summary>
        /// Gets the menu definition for the supplied customization code; the customization menu
        /// replaces the base menu outright.
        /// </summary>
        /// <param name="customizeId">The tenant customization code; empty resolves against the base layer only.</param>
        public MenuSettings GetMenuSettings(string customizeId)
        {
            var custom = !string.IsNullOrEmpty(customizeId) && _customizeReader is not null
                ? _customizeReader.GetCustomizeMenuSettings(customizeId)
                : null;
            // Which layer wins is decided by CustomizeOverlay — the same class a client runs over
            // the two copies it fetched, so both ends select identically.
            return CustomizeOverlay.PickMenuSettings(custom, GetMenuSettings())!;
        }

        /// <summary>
        /// Saves the menu definition.
        /// </summary>
        /// <param name="settings">The menu definition.</param>
        public void SaveMenuSettings(MenuSettings settings)
        {
            // Save the menu through the active storage, then invalidate the cache.
            _storage.SaveMenuSettings(settings);
            _cache.MenuSettings.Remove();
        }

        /// <summary>
        /// Gets the base-layer business plugin bindings.
        /// </summary>
        public PluginSettings GetPluginSettings()
        {
            return _cache.PluginSettings.Get()!;
        }

        /// <summary>
        /// Saves the business plugin bindings.
        /// </summary>
        /// <param name="settings">The plugin bindings.</param>
        public void SavePluginSettings(PluginSettings settings)
        {
            // Save through the active storage, then invalidate the cache.
            _storage.SavePluginSettings(settings);
            _cache.PluginSettings.Remove();
        }

        /// <summary>
        /// Gets the permission model registry.
        /// </summary>
        public PermissionModels GetPermissionModels()
        {
            return _cache.PermissionModels.Get()!;
        }

        /// <summary>
        /// Saves the permission model registry.
        /// </summary>
        /// <param name="models">The permission model registry.</param>
        public void SavePermissionModels(PermissionModels models)
        {
            // Save the permission model registry to file, then invalidate the cache.
            string filePath = _paths.GetPermissionModelsFilePath();
            XmlCodec.SerializeToFile(models, filePath);
            _cache.PermissionModels.Remove();
        }

        /// <summary>
        /// Gets the database category settings.
        /// </summary>
        public DbCategorySettings GetDbCategorySettings()
        {
            return _cache.DbCategorySettings.Get()!;
        }

        /// <summary>
        /// Saves the database category settings.
        /// </summary>
        /// <param name="settings">The database category settings.</param>
        public void SaveDbCategorySettings(DbCategorySettings settings)
        {
            // Save database category settings, then invalidate the cache
            _storage.SaveDbCategorySettings(settings);
            _cache.DbCategorySettings.Remove();
        }

        /// <summary>
        /// Gets the system-level currency master.
        /// </summary>
        public CurrencySettings GetCurrencySettings()
        {
            return _cache.CurrencySettings.Get()!;
        }

        /// <summary>
        /// Saves the system-level currency master.
        /// </summary>
        /// <param name="settings">The currency master.</param>
        public void SaveCurrencySettings(CurrencySettings settings)
        {
            // Save the currency master through the active storage, then invalidate the cache.
            _storage.SaveCurrencySettings(settings);
            _cache.CurrencySettings.Remove();
        }

        /// <summary>
        /// Gets the system-level unit-of-measure master.
        /// </summary>
        public UnitSettings GetUnitSettings()
        {
            return _cache.UnitSettings.Get()!;
        }

        /// <summary>
        /// Saves the system-level unit-of-measure master.
        /// </summary>
        /// <param name="settings">The unit master.</param>
        public void SaveUnitSettings(UnitSettings settings)
        {
            // Save the unit master through the active storage, then invalidate the cache.
            _storage.SaveUnitSettings(settings);
            _cache.UnitSettings.Remove();
        }
    }
}
