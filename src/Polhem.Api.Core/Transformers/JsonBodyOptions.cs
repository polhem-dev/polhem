using System.Text.Json;
using System.Text.Json.Serialization;
using Polhem.Api.Core.Json;
using Polhem.Core.Serialization;

namespace Polhem.Api.Core.Transformers
{
    /// <summary>
    /// The options bodies on the <c>json</c> payload codec are serialized with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The codec exists for clients that cannot reasonably speak the framework's MessagePack
    /// wire — a browser, above all. That wire is assembled from hand-written per-type formatters,
    /// and mirroring them in another language would create a second authority for the same
    /// contract with nothing to catch the two drifting apart.
    /// </para>
    /// <para>
    /// It is a body codec only, and changes nothing else: the payload pipeline stays
    /// serialize → compress → encrypt, and the anti-replay frame is still prepended after
    /// encoding and before encryption.
    /// </para>
    /// <para>
    /// The shape follows what a Plain payload already puts on the wire — same camelCase naming,
    /// same DataSet and DataTable converters, same string-valued enums — so a client has mostly one
    /// JSON shape to understand rather than two.
    /// </para>
    /// <para>
    /// WARNING: "mostly" is load-bearing, and a body written for this codec is <b>not</b> a valid
    /// Plain body. Two differences:
    /// <list type="number">
    /// <item>
    /// <b><c>object</c>-typed members carry a discriminated envelope</b> (<c>[code, value]</c>) via
    /// <see cref="WireValueJsonConverter"/>, which Plain has always lacked. Plain writes and reads
    /// the bare value, read by <see cref="PlainValueJsonConverter"/>. Sending this shape as
    /// Plain does <b>not</b> fail reliably — the member deserializes to a two-element
    /// <c>object[]</c> and travels on, so an <c>In</c> filter would match against the code and the
    /// quoted value with no exception and no log line. The envelope converter cannot simply be added
    /// to the Plain read path either: its reader requires the envelope, so it would break every
    /// client sending bare values.
    /// </item>
    /// <item>
    /// <b>Empty collections are written, not omitted.</b> Plain goes through
    /// <see cref="Polhem.Core.Serialization.JsonCodec"/>, which honours <c>{Property}Specified</c>
    /// properties; these options do not, so a member such as <c>parameters</c> appears as <c>[]</c>.
    /// </item>
    /// </list>
    /// The fixtures under <c>wire-fixtures/</c> are bodies for <b>this</b> codec — the
    /// <c>Encoded</c> and <c>Encrypted</c> paths — and are not Plain request bodies.
    /// </para>
    /// </remarks>
    internal static class JsonBodyOptions
    {
        /// <summary>
        /// The maximum nesting depth accepted from the wire.
        /// </summary>
        /// <remarks>
        /// Taken from the MessagePack codec's own depth limit. Left to the System.Text.Json default
        /// this would be a different number on each wire, which is exactly the kind of difference
        /// that only shows up as a production failure on one of them.
        /// </remarks>
        private const int MaxDepth = Polhem.Api.Core.MessagePack.MessagePackCodec.MaxObjectGraphDepth;

        /// <summary>
        /// WARNING: shared and must stay shared. <see cref="JsonSerializerOptions"/> caches the
        /// contract it builds for each type it meets; a fresh instance per call throws that cache
        /// away and pays full reflection again on every request.
        /// </summary>
        private static readonly JsonSerializerOptions s_options = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                MaxDepth = MaxDepth
            };

            // Keep this list aligned with Polhem.Core.Serialization.JsonCodec: a body that
            // deserialized DataSet, DataTable or enum values differently from a Plain payload
            // would make the same call mean two things depending on the format it arrived in.
            options.Converters.Add(new DataTableJsonConverter());
            options.Converters.Add(new DataSetJsonConverter());
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(WireValueJsonConverter.Instance);

            return options;
        }

        public static JsonSerializerOptions Options => s_options;
    }
}
