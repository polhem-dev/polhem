using System.ComponentModel;
using Polhem.Base.Serialization;
using System.Text;

namespace Polhem.Base.UnitTests
{
    public class GzipTests
    {
        /// <summary>
        /// Tests that data compresses successfully.
        /// </summary>
        [Fact]
        [DisplayName("Compressing and then decompressing restores the original data")]
        public void Compress_ValidInput_ReturnsCompressedBytesThatDecompressCorrectly()
        {
            string originalText = new string('A', 1024);
            byte[] originalBytes = Encoding.UTF8.GetBytes(originalText);

            byte[] compressedData = Gzip.Compress(originalBytes);

            Assert.NotNull(compressedData);
            Assert.NotEmpty(compressedData);

            byte[] decompressedData = Gzip.Decompress(compressedData);
            string decompressedText = Encoding.UTF8.GetString(decompressedData);

            Assert.Equal(originalText, decompressedText);
        }

        /// <summary>
        /// Tests that decompression restores data produced by compression.
        /// </summary>
        [Fact]
        [DisplayName("Decompress restores content identical to the original data")]
        public void Decompress_CompressedInput_ReturnsOriginalData()
        {
            string originalText = "這是要進行解壓縮的測試資料！";
            byte[] originalBytes = Encoding.UTF8.GetBytes(originalText);

            byte[] compressedData = Gzip.Compress(originalBytes);

            byte[] uncompressedData = Gzip.Decompress(compressedData);

            Assert.NotNull(uncompressedData);
            Assert.NotEmpty(uncompressedData);

            string uncompressedText = Encoding.UTF8.GetString(uncompressedData);
            Assert.Equal(originalText, uncompressedText);
        }

        [Fact]
        [DisplayName("Compress uses the Fastest level, producing the same bytes as a Fastest GZipStream")]
        public void Compress_AnyInput_UsesFastestLevel()
        {
            byte[] input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 2000).Select(i => $"row {i} value {i * 7};")));

            byte[] expected;
            using (var stream = new MemoryStream())
            {
                using (var gzip = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionLevel.Fastest, true))
                {
                    gzip.Write(input, 0, input.Length);
                }
                expected = stream.ToArray();
            }

            Assert.Equal(expected, Gzip.Compress(input));
        }

        [Fact]
        [DisplayName("Decompress throws InvalidDataException when the output exceeds the 50 MB limit (zip bomb protection)")]
        public void Decompress_ExceedsMaxSize_ThrowsInvalidDataException()
        {
            // 51 MB of zero bytes compresses very well and exceeds the 50 MB limit once decompressed.
            byte[] payload = new byte[51 * 1024 * 1024];
            byte[] compressed = Gzip.Compress(payload);

            var ex = Assert.Throws<InvalidDataException>(() => Gzip.Decompress(compressed));
            Assert.Contains("Decompressed data exceeds", ex.Message);
        }
    }
}
