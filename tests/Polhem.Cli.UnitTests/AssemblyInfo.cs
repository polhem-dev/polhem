// `ProgramTests` and `DefinesCommandTests` drive the CLI through `Program.Main`, which writes to the process-wide
// `Console.Out` / `Console.Error`. Redirecting them is only safe when no other test class runs at the same time, and
// a class-by-class collection would have to be remembered for every new test that prints, so the whole assembly is
// serialized instead. It holds a few dozen fast tests, so the cost is negligible.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
