using Polhem.Definition.ObjectTree;
using Polhem.Definition.Settings;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>The context-menu commands of the ProgramSettings tree.</summary>
public sealed class ProgramSettingsCommandProvider(ProgramSettingsDocumentViewModel document) : TreeCommandProviderBase(document)
{
    protected override IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node) => node.Value switch
    {
        ProgramSettings => [Create("Program_AddProgram", "IconBox", document.AddProgramCommand)],
        _ => [],
    };
}
