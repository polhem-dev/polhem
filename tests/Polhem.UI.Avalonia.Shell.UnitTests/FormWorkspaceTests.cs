using System.ComponentModel;
using Avalonia.Controls;
using Polhem.UI.Avalonia.Shell.Controls;
using Polhem.UI.Avalonia.Views;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

[Collection(AvaloniaControlsCollection.Name)]
public class FormWorkspaceTests(HeadlessSessionFixture session)
{
    [Fact]
    [DisplayName("A new workspace starts on the list of its program")]
    public Task Constructor_NewWorkspace_ShowsList() => session.RunAsync(() =>
    {
        var workspace = new FormWorkspace("Order");

        var host = Assert.IsType<ContentControl>(workspace.Content);
        var list = Assert.IsType<ListView>(host.Content);
        Assert.Equal("Order", list.ProgId);
    });

    [Fact]
    [DisplayName("A back request while the list shows is not consumed")]
    public Task TryGoBack_ListShowing_ReturnsFalse() => session.RunAsync(() =>
    {
        var workspace = new FormWorkspace("Order");

        Assert.False(workspace.TryGoBack());
    });
}
