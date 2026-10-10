// Some tests here write process-wide statics: `SysInfo.IsDebugMode` (which decides whether an in-process client
// sends Plain), and `SysInfo.Initialize`, which `SystemApiConnector.InitializeAsync` calls. Every connector call
// reads `SysInfo`, so under parallel execution a reader lands inside a writer's try/finally restore window.
//
// Readers grow with new tests, so adding `[Collection]` class by class is bound to miss some. Turning off
// parallelization for the whole assembly is simplest and hard to get wrong (the same approach as
// `Polhem.ObjectCaching.UnitTests` and `Polhem.Api.Core.UnitTests`).
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
