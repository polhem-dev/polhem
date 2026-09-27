using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Polhem.Base.Security
{
    /// <summary>
    /// Encryption and decryption utility using AES-CBC and HMAC authentication.
    /// Uses a 256-bit AES key and a 256-bit HMAC-SHA256 key;
    /// every encryption uses a random IV and appends an integrity verification code (HMAC).
    /// </summary>
    public static class AesCbcHmacCryptor
    {
        // Layout: [ivLength:int32 LE][iv][cipherLength:int32 LE][ciphertext][HMAC-SHA256 over everything before it].
        private const int LengthPrefixSize = sizeof(int);
        private const int IvSize = 16;
        private const int HmacSize = 32;

        /// <summary>
        /// Encrypts data using AES-CBC and appends an HMAC authentication code.
        /// </summary>
        /// <param name="plainBytes">The byte array of the original data.</param>
        /// <param name="aesKey">The AES symmetric encryption key (32 bytes).</param>
        /// <param name="hmacKey">The HMAC verification key (32 bytes).</param>
        /// <returns>The encrypted byte data, containing the IV, ciphertext, and HMAC.</returns>
        /// <remarks>
        /// The whole result is written into one buffer sized up front. The payload pipeline encrypts
        /// every Encrypted body, and building it through a stream and separate arrays allocated about
        /// four times the output, most of it on the large object heap for a list response.
        /// </remarks>
        public static byte[] Encrypt(byte[] plainBytes, byte[] aesKey, byte[] hmacKey)
        {
            ArgumentNullException.ThrowIfNull(plainBytes);

            using var aes = Aes.Create();
            aes.Key = aesKey;

            Span<byte> iv = stackalloc byte[IvSize];
            RandomNumberGenerator.Fill(iv);

            int cipherLength = aes.GetCiphertextLengthCbc(plainBytes.Length, PaddingMode.PKCS7);
            int headerLength = LengthPrefixSize + IvSize + LengthPrefixSize;
            var result = new byte[headerLength + cipherLength + HmacSize];

            BinaryPrimitives.WriteInt32LittleEndian(result, IvSize);
            iv.CopyTo(result.AsSpan(LengthPrefixSize));
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(LengthPrefixSize + IvSize), cipherLength);
            aes.EncryptCbc(plainBytes, iv, result.AsSpan(headerLength, cipherLength), PaddingMode.PKCS7);

            HMACSHA256.HashData(hmacKey, result.AsSpan(0, headerLength + cipherLength),
                result.AsSpan(headerLength + cipherLength, HmacSize));
            return result;
        }

        /// <summary>
        /// Decrypts AES-CBC encrypted data and verifies the HMAC.
        /// </summary>
        /// <param name="encryptedData">The encrypted data.</param>
        /// <param name="aesKey">The AES symmetric encryption key (32 bytes).</param>
        /// <param name="hmacKey">The HMAC verification key (32 bytes).</param>
        /// <returns>The decrypted original data.</returns>
        /// <exception cref="CryptographicException">Thrown when HMAC validation fails or the data format is invalid.</exception>
        public static byte[] Decrypt(byte[] encryptedData, byte[] aesKey, byte[] hmacKey)
        {
            // Minimum: 4 (ivLength) + 16 (IV) + 4 (cipherLength) + 16 (min ciphertext) + 32 (HMAC) = 72
            if (encryptedData == null || encryptedData.Length < 72)
                throw new CryptographicException("Invalid encrypted data.");

            int ivLength = BinaryPrimitives.ReadInt32LittleEndian(encryptedData);
            if (ivLength < 16 || ivLength > 32)
                throw new CryptographicException("Invalid IV length.");

            int cipherLength = BinaryPrimitives.ReadInt32LittleEndian(encryptedData.AsSpan(LengthPrefixSize + ivLength));
            // Remaining bytes after ivLength field (4) + IV + cipherLength field (4) must hold ciphertext + HMAC (32)
            if (cipherLength <= 0 || cipherLength > encryptedData.Length - ivLength - 40)
                throw new CryptographicException("Invalid cipher data length.");

            int headerLength = LengthPrefixSize + ivLength + LengthPrefixSize;
            var authenticated = encryptedData.AsSpan(0, headerLength + cipherLength);
            var storedHmac = encryptedData.AsSpan(headerLength + cipherLength, HmacSize);

            Span<byte> computedHmac = stackalloc byte[HmacSize];
            HMACSHA256.HashData(hmacKey, authenticated, computedHmac);
            if (!CryptographicOperations.FixedTimeEquals(storedHmac, computedHmac))
                throw new CryptographicException("HMAC validation failed.");

            // The length check above admits 17 to 32 bytes, which AES-CBC cannot use as an IV. Rejected
            // after the HMAC check, where the stream-based implementation also failed on it.
            if (ivLength != IvSize)
                throw new CryptographicException("Invalid IV length.");

            using var aes = Aes.Create();
            aes.Key = aesKey;
            return aes.DecryptCbc(encryptedData.AsSpan(headerLength, cipherLength),
                encryptedData.AsSpan(LengthPrefixSize, IvSize), PaddingMode.PKCS7);
        }
    }
}
