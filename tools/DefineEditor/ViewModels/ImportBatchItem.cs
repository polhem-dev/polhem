using CommunityToolkit.Mvvm.ComponentModel;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>A workbook in the batch import wizard.</summary>
public sealed partial class ImportBatchItem : ViewModelBase
{
    /// <param name="path">The full path of the workbook.</param>
    public ImportBatchItem(string path)
    {
        Path = path;
    }

    /// <summary>The full path of the workbook.</summary>
    public string Path { get; }

    /// <summary>The file name, as the list shows it.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(HasFailed))]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    private ImportBatchItemStatus _status;

    /// <summary>The status in the interface language.</summary>
    public string StatusText => L("ImportBatch_Status" + Status);

    public bool HasFailed => Status == ImportBatchItemStatus.Failed;

    public bool IsDone => Status == ImportBatchItemStatus.Done;
}
