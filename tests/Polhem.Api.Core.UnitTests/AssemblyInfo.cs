// `ApiServiceOptionsTests` and `ApiPayloadTransformerTests` have to modify the process-wide statics
// `ApiServiceOptions.PayloadSerializer`, `PayloadCompressor` and `PayloadEncryptor` temporarily to verify how
// `Initialize` assembles them. Meanwhile **every reader of the payload pipeline** (about 19 JSON-RPC round-trip
// test classes at the time) reads the same statics, so running in parallel races.
//
// Originally only the two writer classes were put in the `ApiServiceOptionsState` collection and the readers were
// missed, yet the readers are hit just the same. CI build #31169045420 (2026-08-07) went red for this reason in
// `JsonRpcSerializationTests.JsonRpcRequest_Serialize_ReturnsValidJson`. Encode compressed with
// `GzipPayloadCompressor`, another class swapped the compressor for `NoCompressionCompressor` within that window,
// and Decode fed the gzip bytes unchanged to MessagePack, giving `Unexpected msgpack code 31` (0x1F is the first
// byte of the gzip magic number). The error message points entirely at serialization and does not reveal that the
// root cause is tests contaminating each other.
//
// Readers keep growing with new round-trip tests, so adding `[Collection]` class by class is bound to miss some.
// Disabling parallelization for the whole assembly is the simplest option and hard to get wrong (the same approach
// as in `Polhem.ObjectCaching.UnitTests`). The measured cost is about 0.25 seconds (parallel ~0.40s, serial ~0.66s).
// The real fix is still to move these components into DI.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
