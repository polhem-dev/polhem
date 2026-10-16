// The whole assembly runs serially. It was introduced for the process-wide payload statics of `ApiServiceOptions`
// (removed in 1.2.0; the payload options now come from each test's service provider): their writers and the many
// round-trip readers raced, and CI build #31169045420 (2026-08-07) went red with `Unexpected msgpack code 31`, a gzip
// body decoded after another class had swapped the compressor. The error pointed at serialization, not at tests
// contaminating each other.
//
// Tests here still change other process-wide state (`SysInfo`, see `SysInfoStaticCollection`), and adding
// `[Collection]` class by class is bound to miss a new reader, so the assembly stays serialized. The measured cost at
// the time was about 0.25 seconds (parallel ~0.40s, serial ~0.66s).
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
