using MessagePack.Formatters;

namespace Polhem.Api.Core.MessagePack
{
    internal static partial class WireContracts
    {
        /// <summary>
        /// Registers the definition-layer wire contracts.
        /// </summary>
        private static void AddDefinitionTypes(List<IMessagePackFormatter> list)
        {
            list.Add(WireContract.For<Polhem.Definition.Collections.ListItem>()
                .Member(nameof(Polhem.Definition.Collections.ListItem.Value), static x => x.Value, static (x, v) => x.Value = v)
                .Member(nameof(Polhem.Definition.Collections.ListItem.Text), static x => x.Text, static (x, v) => x.Text = v)
                .Build());
            list.Add(WireContract.For<Polhem.Definition.Collections.Property>()
                .Member(nameof(Polhem.Definition.Collections.Property.Name), static x => x.Name, static (x, v) => x.Name = v)
                .Member(nameof(Polhem.Definition.Collections.Property.Value), static x => x.Value, static (x, v) => x.Value = v)
                .Build());
            list.Add(WireContract.For<Polhem.Definition.Paging.PagingInfo>()
                .Member(nameof(Polhem.Definition.Paging.PagingInfo.Page), static x => x.Page, static (x, v) => x.Page = v)
                .Member(nameof(Polhem.Definition.Paging.PagingInfo.PageSize), static x => x.PageSize, static (x, v) => x.PageSize = v)
                .Member(nameof(Polhem.Definition.Paging.PagingInfo.TotalCount), static x => x.TotalCount, static (x, v) => x.TotalCount = v)
                .Member(nameof(Polhem.Definition.Paging.PagingInfo.HasMore), static x => x.HasMore, static (x, v) => x.HasMore = v)
                .Build());
            list.Add(WireContract.For<Polhem.Definition.Paging.PagingOptions>()
                .Member(nameof(Polhem.Definition.Paging.PagingOptions.Page), static x => x.Page, static (x, v) => x.Page = v)
                .Member(nameof(Polhem.Definition.Paging.PagingOptions.PageSize), static x => x.PageSize, static (x, v) => x.PageSize = v)
                .Member(nameof(Polhem.Definition.Paging.PagingOptions.IncludeTotalCount), static x => x.IncludeTotalCount, static (x, v) => x.IncludeTotalCount = v)
                .Build());
            list.Add(WireContract.For<Polhem.Definition.Security.ApiKeySummary>()
                .Member(nameof(Polhem.Definition.Security.ApiKeySummary.SysId), static x => x.SysId, static (x, v) => x.SysId = v)
                .Member(nameof(Polhem.Definition.Security.ApiKeySummary.SysName), static x => x.SysName, static (x, v) => x.SysName = v)
                .Member(nameof(Polhem.Definition.Security.ApiKeySummary.KeyType), static x => x.KeyType, static (x, v) => x.KeyType = v)
                .Member(nameof(Polhem.Definition.Security.ApiKeySummary.Contact), static x => x.Contact, static (x, v) => x.Contact = v)
                .Member(nameof(Polhem.Definition.Security.ApiKeySummary.Enabled), static x => x.Enabled, static (x, v) => x.Enabled = v)
                .Member(nameof(Polhem.Definition.Security.ApiKeySummary.ExpiredAt), static x => x.ExpiredAt, static (x, v) => x.ExpiredAt = v)
                .Member(nameof(Polhem.Definition.Security.ApiKeySummary.IssuedAt), static x => x.IssuedAt, static (x, v) => x.IssuedAt = v)
                .Build());
            list.Add(WireContract.For<Polhem.Definition.Settings.CurrencyItem>()
                .Member(nameof(Polhem.Definition.Settings.CurrencyItem.Code), static x => x.Code, static (x, v) => x.Code = v)
                .Member(nameof(Polhem.Definition.Settings.CurrencyItem.Numeric), static x => x.Numeric, static (x, v) => x.Numeric = v)
                .Member(nameof(Polhem.Definition.Settings.CurrencyItem.Rounding), static x => x.Rounding, static (x, v) => x.Rounding = v)
                .Member(nameof(Polhem.Definition.Settings.CurrencyItem.Symbol), static x => x.Symbol, static (x, v) => x.Symbol = v)
                .Member(nameof(Polhem.Definition.Settings.CurrencyItem.Name), static x => x.Name, static (x, v) => x.Name = v)
                .Build());
            list.Add(WireContract.For<Polhem.Definition.Settings.UnitItem>()
                .Member(nameof(Polhem.Definition.Settings.UnitItem.Code), static x => x.Code, static (x, v) => x.Code = v)
                .Member(nameof(Polhem.Definition.Settings.UnitItem.Decimals), static x => x.Decimals, static (x, v) => x.Decimals = v)
                .Member(nameof(Polhem.Definition.Settings.UnitItem.Dimension), static x => x.Dimension, static (x, v) => x.Dimension = v)
                .Member(nameof(Polhem.Definition.Settings.UnitItem.Name), static x => x.Name, static (x, v) => x.Name = v)
                .Build());

            list.Add(new KeyCollectionBaseFormatter<Polhem.Definition.Collections.ListItemCollection, Polhem.Definition.Collections.ListItem>());
            list.Add(new KeyCollectionBaseFormatter<Polhem.Definition.Collections.PropertyCollection, Polhem.Definition.Collections.Property>());
            list.Add(new CollectionBaseFormatter<Polhem.Definition.CompanyAllowedCurrencies, Polhem.Definition.AllowedCurrencyItem>());
            list.Add(new CollectionBaseFormatter<Polhem.Definition.CompanyCashRounding, Polhem.Definition.CashRoundingItem>());
            list.Add(new CollectionBaseFormatter<Polhem.Definition.CompanyNumberFormats, Polhem.Definition.NumberFormatItem>());
            list.Add(new CollectionBaseFormatter<Polhem.Definition.Filters.FilterNodeCollection, Polhem.Definition.Filters.FilterNode>());
            list.Add(new CollectionBaseFormatter<Polhem.Definition.Organization.DepartmentNodeCollection, Polhem.Definition.Organization.DepartmentNode>());
            list.Add(new CollectionBaseFormatter<Polhem.Definition.Settings.CurrencySettings, Polhem.Definition.Settings.CurrencyItem>());
            list.Add(new CollectionBaseFormatter<Polhem.Definition.Settings.UnitSettings, Polhem.Definition.Settings.UnitItem>());
            list.Add(new CollectionBaseFormatter<Polhem.Definition.Sorting.SortFieldCollection, Polhem.Definition.Sorting.SortField>());
        }
    }
}
