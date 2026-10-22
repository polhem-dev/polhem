namespace Polhem.DefineEditor.ViewModels;

/// <summary>Where a workbook of the batch import stands.</summary>
public enum ImportBatchItemStatus
{
    /// <summary>Not imported yet.</summary>
    Pending,

    /// <summary>Being read and written.</summary>
    Importing,

    /// <summary>Its tables are being upgraded.</summary>
    Upgrading,

    /// <summary>Imported, and upgraded when that was asked for.</summary>
    Done,

    /// <summary>The batch stopped here: the workbook has errors or its upgrade failed.</summary>
    Failed,

    /// <summary>The batch stopped before reaching it.</summary>
    NotRun,
}
