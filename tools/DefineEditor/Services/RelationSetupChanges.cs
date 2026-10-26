using Polhem.Definition.Forms;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// What <see cref="RelationSetup.Apply"/> changed besides the relation itself.
/// </summary>
/// <param name="AddedFields">The destination fields added to the table, in table order.</param>
/// <param name="RemovedFields">The relation fields removed because no mapping writes them any more.</param>
/// <param name="ForeignKeyRetyped">Whether the foreign key field was changed to <see cref="Polhem.Core.Data.FieldDbType.Guid"/>.</param>
public sealed record RelationSetupChanges(
    IReadOnlyList<FormField> AddedFields,
    IReadOnlyList<FormField> RemovedFields,
    bool ForeignKeyRetyped);
