namespace Polhem.DefineEditor.ViewModels;

/// <summary>What a run of the database manager does, which decides the window's title and its status texts.</summary>
public enum DbCommandKind
{
    /// <summary>Builds or upgrades tables (<c>upgrade</c>).</summary>
    Upgrade,

    /// <summary>Lists the statements an upgrade would run, without running them (<c>upgrade --dry-run</c>).</summary>
    Preview,

    /// <summary>Opens a connection to each database (<c>check</c>).</summary>
    Check,
}
