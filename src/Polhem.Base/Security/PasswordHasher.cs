using System.Globalization;
using System.Security.Cryptography;

namespace Polhem.Base.Security
{
    /// <summary>
    /// Utility class for password hashing and verification using PBKDF2-SHA256.
    /// Stored form: <c>v2.{iterations}.{saltBase64}.{hashBase64}</c>.
    /// </summary>
    /// <remarks>
    /// The stored form carries its own iteration count, so raising <see cref="Iterations"/> leaves
    /// existing hashes verifiable. A caller that has just verified a password should consult
    /// <see cref="NeedsRehash"/> and store a fresh hash when it returns <c>true</c>; that is how
    /// stored parameters catch up with the current ones without a forced password reset.
    /// <para>
    /// Only the <c>v2.</c> format verifies. The unprefixed PBKDF2-SHA1 format inherited from Bee.NET is
    /// rejected like any other malformed value: an account still holding one needs a new password.
    /// </para>
    /// </remarks>
    public static class PasswordHasher
    {
        private const int SaltSize = 16; // 128-bit
        private const int HashSize = 32; // 256-bit

        /// <summary>
        /// The PBKDF2-SHA256 iteration count used for new hashes, following the OWASP password storage
        /// recommendation for that algorithm.
        /// </summary>
        public static readonly int Iterations = 600_000;

        private const string V2Prefix = "v2.";

        /// <summary>
        /// Creates a hashed password string in the <c>v2.{iterations}.{saltBase64}.{hashBase64}</c>
        /// format, using PBKDF2-SHA256 with <see cref="Iterations"/> iterations.
        /// </summary>
        /// <param name="password">The original password.</param>
        /// <returns>The hashed password string.</returns>
        public static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = PBKDF2SHA256(password, salt, Iterations, HashSize);
            return $"{V2Prefix}{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verifies whether the provided password matches the stored hash.
        /// </summary>
        /// <param name="password">The password entered by the user.</param>
        /// <param name="hashedPassword">The stored hashed password string.</param>
        /// <returns>
        /// True if the password matches; otherwise, false. A value that is not in the <c>v2.</c> format,
        /// including the retired PBKDF2-SHA1 format, never matches.
        /// </returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            if (!TryParse(hashedPassword, out int iterations, out byte[] salt, out byte[] storedHash))
            {
                return false;
            }

            try
            {
                var computedHash = PBKDF2SHA256(password, salt, iterations, storedHash.Length);
                return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
            }
            catch (ArgumentException)
            {
                // Invalid PBKDF2 parameters in the stored value (an iteration count of zero or less)
                // mean the password cannot match — fail closed.
                return false;
            }
        }

        /// <summary>
        /// Determines whether a stored hash was produced with weaker parameters than
        /// <see cref="HashPassword"/> uses today, and should be replaced after a successful verification.
        /// </summary>
        /// <param name="hashedPassword">The stored hashed password string.</param>
        /// <returns>
        /// <c>true</c> when the value is a <c>v2.</c> hash with fewer iterations, a shorter salt or a
        /// shorter hash than the current parameters, or cannot be parsed at all; otherwise <c>false</c>.
        /// </returns>
        public static bool NeedsRehash(string hashedPassword)
        {
            if (!TryParse(hashedPassword, out int iterations, out byte[] salt, out byte[] storedHash))
            {
                return true;
            }
            return iterations < Iterations || salt.Length < SaltSize || storedHash.Length < HashSize;
        }

        /// <summary>
        /// Splits a stored <c>v2.</c> value into its parameters.
        /// </summary>
        /// <returns><c>false</c> when the value is not a well-formed, usable <c>v2.</c> hash.</returns>
        private static bool TryParse(string? hashedPassword, out int iterations, out byte[] salt, out byte[] storedHash)
        {
            iterations = 0;
            salt = [];
            storedHash = [];
            if (hashedPassword == null || !hashedPassword.StartsWith(V2Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var parts = hashedPassword[V2Prefix.Length..].Split('.');
            if (parts.Length != 3) { return false; }

            try
            {
                iterations = int.Parse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture);
                salt = Convert.FromBase64String(parts[1]);
                storedHash = Convert.FromBase64String(parts[2]);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                // A malformed stored hash (bad base64, unparsable iteration count) means the password
                // cannot match — fail closed. Unexpected exceptions are left to propagate rather than
                // masquerading as a wrong password.
                return false;
            }

            return iterations > 0 && IsUsableStoredHash(salt, storedHash);
        }

        /// <summary>
        /// Determines whether the components parsed out of a stored hash can represent a real hash.
        /// </summary>
        /// <param name="salt">The salt parsed from the stored value.</param>
        /// <param name="storedHash">The hash parsed from the stored value.</param>
        /// <remarks>
        /// WARNING: without this an empty hash segment authenticates every password. PBKDF2 asked for
        /// zero output bytes returns an empty array, and
        /// <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
        /// reports two empty spans as equal — so a stored value of <c>v2.100000..</c> would verify
        /// against anything.
        /// <para>
        /// The iteration count is deliberately not floored here. A low count weakens offline cracking
        /// of that one password, but reaching it already requires the ability to write the stored
        /// value, and a floor would lock out hashes created before <see cref="Iterations"/> was raised.
        /// <see cref="NeedsRehash"/> is what moves such a hash forward on the next successful sign-in.
        /// </para>
        /// </remarks>
        private static bool IsUsableStoredHash(byte[] salt, byte[] storedHash)
            => salt.Length > 0 && storedHash.Length > 0;

        /// <summary>
        /// Generates a PBKDF2-SHA256 hash.
        /// </summary>
        private static byte[] PBKDF2SHA256(string password, byte[] salt, int iterations, int outputBytes)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                System.Text.Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, outputBytes);
        }
    }
}
