using MessagePack.Formatters;

namespace Polhem.Api.Core.MessagePack
{
    internal static partial class WireContracts
    {
        /// <summary>
        /// Registers the shared envelope and cross-area wire contracts.
        /// </summary>
        private static void AddEnvelopeMessages(List<IMessagePackFormatter> list)
        {
            list.Add(WireContract.For<Polhem.Api.Contracts.AuditLog.RecordFieldChange>()
                .Member(nameof(Polhem.Api.Contracts.AuditLog.RecordFieldChange.TableName), static x => x.TableName, static (x, v) => x.TableName = v)
                .Member(nameof(Polhem.Api.Contracts.AuditLog.RecordFieldChange.RowKey), static x => x.RowKey, static (x, v) => x.RowKey = v)
                .Member(nameof(Polhem.Api.Contracts.AuditLog.RecordFieldChange.RowState), static x => x.RowState, static (x, v) => x.RowState = v)
                .Member(nameof(Polhem.Api.Contracts.AuditLog.RecordFieldChange.FieldName), static x => x.FieldName, static (x, v) => x.FieldName = v)
                .Member(nameof(Polhem.Api.Contracts.AuditLog.RecordFieldChange.OldValue), static x => x.OldValue, static (x, v) => x.OldValue = v)
                .Member(nameof(Polhem.Api.Contracts.AuditLog.RecordFieldChange.NewValue), static x => x.NewValue, static (x, v) => x.NewValue = v)
                .Build());
            list.Add(WireContract.For<Polhem.Api.Core.Messages.ExecFuncRequest>()
                .Member(nameof(Polhem.Api.Core.Messages.ExecFuncRequest.Parameters), static x => x.Parameters, static (x, v) => x.Parameters = v)
                .Member(nameof(Polhem.Api.Core.Messages.ExecFuncRequest.FuncId), static x => x.FuncId, static (x, v) => x.FuncId = v)
                .Build());
            list.Add(WireContract.For<Polhem.Api.Core.Messages.ExecFuncResponse>()
                .Member(nameof(Polhem.Api.Core.Messages.ExecFuncResponse.Parameters), static x => x.Parameters, static (x, v) => x.Parameters = v)
                .Build());
        }
    }
}
