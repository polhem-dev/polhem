
namespace Polhem.Api.Core.Messages
{
    /// <summary>
    /// The transport payload encoding format.
    /// </summary>
    public enum PayloadFormat
    {
        /// <summary>
        /// Plain format (not encoded or encrypted).
        /// </summary>
        Plain,

        /// <summary>
        /// Encoded format (serialized and compressed).
        /// </summary>
        Encoded,

        /// <summary>
        /// Encrypted format (serialized + compressed + encrypted).
        /// </summary>
        /// <remarks>
        /// IMPORTANT: this relies on TLS for protection against an active man-in-the-middle. The
        /// session key is delivered at login under a client public key that nothing authenticates,
        /// and the configuration a client reads before login travels as Plain, so over plain HTTP an
        /// intermediary can substitute either. Within TLS it protects the payload from observers of
        /// the decrypted stream, such as a TLS-terminating proxy, and is the only format whose
        /// anti-replay frame is authenticated.
        /// </remarks>
        Encrypted
    }
}
