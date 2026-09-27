using MessagePack.Formatters;

namespace Polhem.Api.Core.MessagePack
{
    internal static partial class WireContracts
    {
        /// <summary>
        /// Registers the closed generic instantiations the wire needs.
        /// </summary>
        private static void AddGenericInstantiations(List<IMessagePackFormatter> list)
        {
            list.Add(new WireEnumFormatter<Polhem.Definition.DefineType>());
            list.Add(new WireEnumFormatter<Polhem.Definition.Filters.ComparisonOperator>());
            list.Add(new WireEnumFormatter<Polhem.Definition.Filters.LogicalOperator>());
            list.Add(new WireEnumFormatter<Polhem.Definition.Logging.AnomalyKind>());
            list.Add(new WireEnumFormatter<Polhem.Definition.Logging.ChangeKind>());
            list.Add(new WireEnumFormatter<Polhem.Definition.Logging.LoginEvent>());
            list.Add(new WireEnumFormatter<Polhem.Definition.NumberKind>());
            list.Add(new WireEnumFormatter<Polhem.Definition.Security.ApiKeyStatus>());
            list.Add(new WireEnumFormatter<Polhem.Definition.Security.ApiKeyType>());
            list.Add(new WireEnumFormatter<Polhem.Definition.Settings.PermissionActions>());
            list.Add(new WireEnumFormatter<Polhem.Definition.Sorting.SortDirection>());

            list.Add(new NullableFormatter<Polhem.Definition.Logging.AnomalyKind>());
            list.Add(new NullableFormatter<Polhem.Definition.Logging.ChangeKind>());
            list.Add(new NullableFormatter<Polhem.Definition.Logging.LoginEvent>());
            list.Add(new NullableFormatter<System.DateTime>());
            list.Add(new NullableFormatter<System.Int32>());

            list.Add(new ListFormatter<Polhem.Api.Contracts.AuditLog.RecordFieldChange>());
            list.Add(new ListFormatter<Polhem.Definition.Security.ApiKeySummary>());

            list.Add(new DictionaryFormatter<System.String, Polhem.Definition.Settings.PermissionActions>());
            list.Add(new DictionaryFormatter<System.String, System.Int32>());

            list.Add(new ArrayFormatter<System.String>());
        }
    }
}
