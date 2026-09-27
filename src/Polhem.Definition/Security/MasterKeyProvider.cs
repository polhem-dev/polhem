using Polhem.Definition.Settings;
using Polhem.Base;
using Polhem.Base.Security;

namespace Polhem.Definition.Security
{
    /// <summary>
    /// Master key provider that loads the master key from a configured source.
    /// </summary>
    public static class MasterKeyProvider
    {
        private const string DefaultEnvironmentVariable = "POLHEM_MASTER_KEY";
        private const string BeeEnvironmentVariable = "BEE_MASTER_KEY";
        private const int ReadRetryCount = 5;
        private const int ReadRetryDelayMs = 50;

        /// <summary>
        /// Gets the master key content.
        /// </summary>
        /// <param name="source">The master key source configuration.</param>
        /// <param name="definePath">
        /// Root directory used to resolve a relative <see cref="MasterKeySourceType.File"/>
        /// path (typically the configured <c>DefinePath</c>). Ignored for environment-variable
        /// sources or when <see cref="MasterKeySource.Value"/> is absolute.
        /// </param>
        /// <param name="autoCreate">Indicates whether to automatically create the master key if it does not exist.</param>
        /// <returns>The decoded master key as a byte array.</returns>
        public static byte[] GetMasterKey(MasterKeySource source, string definePath, bool autoCreate = false)
        {
            string keyText;

            switch (source.Type)
            {
                case MasterKeySourceType.File:
                    keyText = LoadFromFile(source.Value, definePath, autoCreate);
                    break;

                case MasterKeySourceType.Environment:
                    keyText = LoadFromEnvironment(source.Value, autoCreate);
                    break;

                default:
                    throw new InvalidOperationException("Unsupported master key source type.");
            }

            if (string.IsNullOrWhiteSpace(keyText))
                throw new InvalidOperationException("Master key is empty or not found.");

            try
            {
                return Convert.FromBase64String(keyText.Trim());
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Master key is not valid Base64 format.", ex);
            }
        }

        /// <summary>
        /// Loads the master key content from a file.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="definePath">Root directory used to resolve relative <paramref name="filePath"/> values.</param>
        /// <param name="autoCreate">Indicates whether to automatically create the master key if the file does not exist.</param>
        /// <returns>The master key content.</returns>
        private static string LoadFromFile(string filePath, string definePath, bool autoCreate)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                filePath = "Master.key";
            }

            // If the path is relative, prepend the configured define path.
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.Combine(definePath ?? string.Empty, filePath);
            }

            if (!File.Exists(filePath))
            {
                if (!autoCreate)
                    throw new FileNotFoundException("Master key file not found: " + filePath);

                // Atomically create the file so concurrent callers (e.g. parallel test hosts
                // sharing a Define folder) don't overwrite each other's keys. If another
                // process wins the race, fall through to read the key it just wrote. The file is
                // created owner-only in the same step (see FileWriteOwnerOnlyText), so the key never
                // sits in a file with default permissions, not even briefly.
                string newKey = GenerateNewKey();
                try
                {
                    FileUtilities.FileWriteOwnerOnlyText(filePath, newKey, overwrite: false);
                    return newKey;
                }
                catch (IOException) when (File.Exists(filePath))
                {
                    // Another process created the file between our File.Exists check and the
                    // create-if-absent move; fall through to read the winning key.
                }
            }

            return ReadAllTextShared(filePath);
        }

        /// <summary>
        /// Reads the entire file with <see cref="FileShare.ReadWrite"/> so concurrent
        /// readers do not collide with the brief write/truncate lock held by another
        /// process during file creation. Retries on transient <see cref="IOException"/>.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        private static string ReadAllTextShared(string filePath)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(fs);
                    return reader.ReadToEnd();
                }
                catch (IOException) when (attempt < ReadRetryCount - 1)
                {
                    Thread.Sleep(ReadRetryDelayMs);
                    attempt++;
                }
            }
        }

        /// <summary>
        /// Loads the master key content from an environment variable.
        /// </summary>
        /// <param name="varName">The environment variable name.</param>
        /// <param name="autoCreate">Indicates whether to automatically create the master key if the variable is not set.</param>
        /// <returns>The master key content.</returns>
        private static string LoadFromEnvironment(string varName, bool autoCreate)
        {
            if (string.IsNullOrWhiteSpace(varName))
            {
                varName = DefaultEnvironmentVariable;
            }

            string? value = Environment.GetEnvironmentVariable(varName);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (autoCreate)
                {
                    string newKey = GenerateNewKey();
                    Environment.SetEnvironmentVariable(varName, newKey);
                    return newKey;
                }
                throw new InvalidOperationException(DescribeMissingVariable(varName));
            }

            return value;
        }

        /// <summary>
        /// Builds the error for a master key variable that is not set, pointing out the Bee.NET
        /// default when that one is set instead.
        /// </summary>
        /// <param name="varName">The variable that was looked up.</param>
        private static string DescribeMissingVariable(string varName)
        {
            string message = "Environment variable '" + varName + "' not found.";
            if (string.Equals(varName, DefaultEnvironmentVariable, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BeeEnvironmentVariable)))
            {
                message += " " + BeeEnvironmentVariable + " is set, which is the Bee.NET default: rename it to " +
                    DefaultEnvironmentVariable + ", or name it in the MasterKeySource of SystemSettings.xml. " +
                    "See \"Migrating from Bee.NET\" in the Polhem README.";
            }
            return message;
        }

        /// <summary>
        /// Generates a new Base64-encoded master key.
        /// </summary>
        private static string GenerateNewKey()
        {
            // Generate a new Base64-encoded master key
            return AesCbcHmacKeyGenerator.GenerateBase64CombinedKey();
        }
    }

}
