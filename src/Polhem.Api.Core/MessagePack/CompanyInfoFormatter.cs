using Polhem.Definition;
using Polhem.Definition.Identity;
using MessagePack;
using MessagePack.Formatters;

namespace Polhem.Api.Core.MessagePack
{
    /// <summary>
    /// Serializes <see cref="CompanyInfo"/> as a property-name keyed map.
    /// </summary>
    /// <remarks>
    /// Hand-written because the properties are init-only: the generic <see cref="WireContract"/>
    /// builder assigns each member to an instance that already exists, which an init accessor does
    /// not allow. Reading into locals and constructing once keeps the type immutable after load.
    /// <para>
    /// WARNING: Adding a property to <see cref="CompanyInfo"/> means adding it here too. The guard is
    /// <c>WireContractDriftTests</c>, which compares <see cref="WireMemberNames"/> against the
    /// type's actual shape, and <c>WireCodecParityTests</c>, which catches a member missing from
    /// the <c>Serialize</c> or <c>Deserialize</c> body.
    /// </para>
    /// </remarks>
    internal sealed class CompanyInfoFormatter : IMessagePackFormatter<CompanyInfo?>, IWireContract
    {
        private static readonly string[] s_wireMembers =
        [
            nameof(CompanyInfo.CompanyId),
            nameof(CompanyInfo.CompanyName),
            nameof(CompanyInfo.CompanyDatabaseId),
            nameof(CompanyInfo.CustomizeId),
            nameof(CompanyInfo.NumberFormats),
            nameof(CompanyInfo.DefaultCurrency),
            nameof(CompanyInfo.CashRounding),
            nameof(CompanyInfo.AllowedCurrencies),
        ];

        /// <inheritdoc />
        public Type WireType => typeof(CompanyInfo);

        /// <inheritdoc />
        public IReadOnlyList<string> WireMemberNames => s_wireMembers;

        /// <summary>
        /// Serializes the value.
        /// </summary>
        public void Serialize(ref MessagePackWriter writer, CompanyInfo? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(s_wireMembers.Length);

            writer.Write(nameof(CompanyInfo.CompanyId));
            MessagePackSerializer.Serialize<string>(ref writer, value.CompanyId, options);

            writer.Write(nameof(CompanyInfo.CompanyName));
            MessagePackSerializer.Serialize<string>(ref writer, value.CompanyName, options);

            writer.Write(nameof(CompanyInfo.CompanyDatabaseId));
            MessagePackSerializer.Serialize<string>(ref writer, value.CompanyDatabaseId, options);

            writer.Write(nameof(CompanyInfo.CustomizeId));
            MessagePackSerializer.Serialize<string>(ref writer, value.CustomizeId, options);

            writer.Write(nameof(CompanyInfo.NumberFormats));
            MessagePackSerializer.Serialize<CompanyNumberFormats>(ref writer, value.NumberFormats, options);

            writer.Write(nameof(CompanyInfo.DefaultCurrency));
            MessagePackSerializer.Serialize<string>(ref writer, value.DefaultCurrency, options);

            writer.Write(nameof(CompanyInfo.CashRounding));
            MessagePackSerializer.Serialize<CompanyCashRounding>(ref writer, value.CashRounding, options);

            writer.Write(nameof(CompanyInfo.AllowedCurrencies));
            MessagePackSerializer.Serialize<CompanyAllowedCurrencies>(ref writer, value.AllowedCurrencies, options);
        }

        /// <summary>
        /// Deserializes the value, skipping keys this version does not know. A member absent from
        /// the payload keeps the default a new instance would have.
        /// </summary>
        public CompanyInfo? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            options.Security.DepthStep(ref reader);
            try
            {
                var defaults = new CompanyInfo();
                string companyId = defaults.CompanyId;
                string companyName = defaults.CompanyName;
                string companyDatabaseId = defaults.CompanyDatabaseId;
                string customizeId = defaults.CustomizeId;
                CompanyNumberFormats numberFormats = defaults.NumberFormats;
                string defaultCurrency = defaults.DefaultCurrency;
                CompanyCashRounding cashRounding = defaults.CashRounding;
                CompanyAllowedCurrencies allowedCurrencies = defaults.AllowedCurrencies;

                var count = reader.ReadMapHeader();
                for (var i = 0; i < count; i++)
                {
                    switch (reader.ReadString())
                    {
                        case nameof(CompanyInfo.CompanyId):
                            companyId = MessagePackSerializer.Deserialize<string>(ref reader, options);
                            break;
                        case nameof(CompanyInfo.CompanyName):
                            companyName = MessagePackSerializer.Deserialize<string>(ref reader, options);
                            break;
                        case nameof(CompanyInfo.CompanyDatabaseId):
                            companyDatabaseId = MessagePackSerializer.Deserialize<string>(ref reader, options);
                            break;
                        case nameof(CompanyInfo.CustomizeId):
                            customizeId = MessagePackSerializer.Deserialize<string>(ref reader, options);
                            break;
                        case nameof(CompanyInfo.NumberFormats):
                            numberFormats = MessagePackSerializer.Deserialize<CompanyNumberFormats>(ref reader, options);
                            break;
                        case nameof(CompanyInfo.DefaultCurrency):
                            defaultCurrency = MessagePackSerializer.Deserialize<string>(ref reader, options);
                            break;
                        case nameof(CompanyInfo.CashRounding):
                            cashRounding = MessagePackSerializer.Deserialize<CompanyCashRounding>(ref reader, options);
                            break;
                        case nameof(CompanyInfo.AllowedCurrencies):
                            allowedCurrencies = MessagePackSerializer.Deserialize<CompanyAllowedCurrencies>(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                return new CompanyInfo
                {
                    CompanyId = companyId,
                    CompanyName = companyName,
                    CompanyDatabaseId = companyDatabaseId,
                    CustomizeId = customizeId,
                    NumberFormats = numberFormats,
                    DefaultCurrency = defaultCurrency,
                    CashRounding = cashRounding,
                    AllowedCurrencies = allowedCurrencies,
                };
            }
            finally
            {
                reader.Depth--;
            }
        }
    }
}
