using System.ComponentModel;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// How the preview engine stored in the user's settings is read back, including the name written before the web
    /// preview offered a second package.
    /// </summary>
    public class PreviewEngineSettingTests
    {
        [Theory]
        [InlineData("Avalonia", FormLayoutPreviewEngine.Avalonia)]
        [InlineData("Mud", FormLayoutPreviewEngine.Mud)]
        [InlineData("Fluent", FormLayoutPreviewEngine.Fluent)]
        [DisplayName("A stored engine name reads back as that engine")]
        public void ParsePreviewEngine_KnownName_ReturnsThatEngine(string name, FormLayoutPreviewEngine expected)
        {
            Assert.Equal(expected, DocumentViewModelFactory.ParsePreviewEngine(name));
        }

        [Fact]
        [DisplayName("The Web engine of an older setting reads as MudBlazor, the package it drew with")]
        public void ParsePreviewEngine_LegacyWeb_ReturnsMud()
        {
            Assert.Equal(FormLayoutPreviewEngine.Mud, DocumentViewModelFactory.ParsePreviewEngine("Web"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Wasm")]
        [InlineData("7")]
        [DisplayName("A missing or unknown engine name falls back to the Avalonia preview")]
        public void ParsePreviewEngine_UnknownName_ReturnsAvalonia(string? name)
        {
            Assert.Equal(FormLayoutPreviewEngine.Avalonia, DocumentViewModelFactory.ParsePreviewEngine(name));
        }
    }
}
