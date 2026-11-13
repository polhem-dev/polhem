namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// Serializes the test classes that construct Avalonia controls and shares the headless platform they run on.
/// Avalonia's property dictionary pool is not thread-safe, so two classes building controls at the same moment can
/// fail intermittently.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AvaloniaControlsCollection : ICollectionFixture<HeadlessSessionFixture>
{
    /// <summary>The collection name. Reference this constant instead of repeating the string.</summary>
    public const string Name = "AvaloniaControls";
}
