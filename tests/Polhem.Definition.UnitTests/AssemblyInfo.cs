// The whole assembly is serialized, instead of putting [Collection] on each class.
//
// Several test classes in this assembly touch the same process-wide state (the `POLHEM_MASTER_KEY`
// environment variable, `GlobalEvents`, and DI containers built inside test bodies); see the comment
// on `ProcessWideStateCollection`.
//
// Putting [Collection] on each class relies on remembering to add it with every new test, and the readers
// keep growing as tests are added. Such a requirement is bound to be missed, and when it is, the tests look
// serialized but are not, with no compile or test signal.
// Serializing once at the assembly level makes it structural. `ProcessWideStateCollection` is therefore
// redundant, but it is kept as a record of which classes touch process-wide state.
//
// Measured cost (2026-09-04, 1,086 tests): 352-634 ms before serializing, 723-823 ms after.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
