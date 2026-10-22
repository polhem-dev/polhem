namespace Polhem.DefineEditor.ViewModels;

/// <summary>The steps of the batch import wizard, in order.</summary>
public enum ImportBatchStep
{
    /// <summary>Choose the workbooks and their order.</summary>
    Files,

    /// <summary>Choose how existing files are treated and whether the database is upgraded.</summary>
    Options,

    /// <summary>Review the order and the options before starting.</summary>
    Confirm,

    /// <summary>The workbooks are imported one by one.</summary>
    Run,
}
