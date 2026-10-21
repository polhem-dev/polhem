using System.ComponentModel;
using Polhem.UI.Avalonia.Shell.Preferences;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

public sealed class FileShellPreferenceStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "polhem-shell-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    [DisplayName("Save then Load returns the stored value from a new instance")]
    public void Save_ThenLoad_RoundTrips()
    {
        new FileShellPreferenceStorage("App", _root).Save(ShellPreferenceKeys.CompanyId, "NORTHWIND");

        var value = new FileShellPreferenceStorage("App", _root).Load(ShellPreferenceKeys.CompanyId);

        Assert.Equal("NORTHWIND", value);
    }

    [Fact]
    [DisplayName("Save creates the application folder when it does not exist")]
    public void Save_MissingFolder_CreatesIt()
    {
        var storage = new FileShellPreferenceStorage("App", _root);

        storage.Save(ShellPreferenceKeys.Language, "zh-TW");

        Assert.True(Directory.Exists(storage.DirectoryPath));
    }

    [Fact]
    [DisplayName("Load returns an empty string for a key that was never saved")]
    public void Load_UnknownKey_ReturnsEmpty()
    {
        var value = new FileShellPreferenceStorage("App", _root).Load(ShellPreferenceKeys.Language);

        Assert.Equal(string.Empty, value);
    }

    [Fact]
    [DisplayName("Save with an empty value removes the stored preference")]
    public void Save_EmptyValue_RemovesPreference()
    {
        var storage = new FileShellPreferenceStorage("App", _root);
        storage.Save(ShellPreferenceKeys.Language, "en");

        storage.Save(ShellPreferenceKeys.Language, string.Empty);

        Assert.Equal(string.Empty, storage.Load(ShellPreferenceKeys.Language));
    }

    [Fact]
    [DisplayName("Save with an empty value before anything was stored does not throw")]
    public void Save_EmptyValueWithoutFolder_DoesNotThrow()
    {
        var storage = new FileShellPreferenceStorage("App", _root);

        storage.Save(ShellPreferenceKeys.Language, string.Empty);

        Assert.False(Directory.Exists(storage.DirectoryPath));
    }

    [Theory]
    [DisplayName("A key or an application name that would leave the folder is rejected")]
    [InlineData("App", "../escape")]
    [InlineData("../App", "language")]
    public void Constructor_OrKey_PathOutsideFolder_Throws(string appName, string key)
    {
        Assert.Throws<ArgumentException>(() => new FileShellPreferenceStorage(appName, _root).Load(key));
    }
}
