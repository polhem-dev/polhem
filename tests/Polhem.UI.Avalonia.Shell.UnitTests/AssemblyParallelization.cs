// Run this assembly's tests serially.
//
// The view tests run on the Avalonia headless platform, which binds Avalonia's UI dispatcher to its own thread for
// each test. A test of another class that builds an Avalonia object on a test thread at the same moment would bind the
// dispatcher to that thread instead, and the headless test fails with "a different thread owns it". The assembly runs
// in about a second, so serial execution costs little.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
