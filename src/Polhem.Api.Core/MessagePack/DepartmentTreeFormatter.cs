using Polhem.Definition.Organization;
using MessagePack;
using MessagePack.Formatters;

namespace Polhem.Api.Core.MessagePack
{
    /// <summary>
    /// Serializes <see cref="DepartmentTree"/> as a property-name keyed map.
    /// </summary>
    /// <remarks>
    /// Hand-written because the properties are init-only: the generic <see cref="WireContract"/>
    /// builder assigns each member to an instance that already exists, which an init accessor does
    /// not allow.
    /// <para>
    /// WARNING: Adding a property to <see cref="DepartmentTree"/> means adding it here too. The guard
    /// is <c>WireContractDriftTests</c>, which compares <see cref="WireMemberNames"/> against the
    /// type's actual shape, and <c>WireCodecParityTests</c>, which catches a member missing from
    /// the <c>Serialize</c> or <c>Deserialize</c> body.
    /// </para>
    /// </remarks>
    internal sealed class DepartmentTreeFormatter : IMessagePackFormatter<DepartmentTree?>, IWireContract
    {
        private static readonly string[] s_wireMembers =
        [
            nameof(DepartmentTree.CompanyId),
            nameof(DepartmentTree.Roots),
        ];

        /// <inheritdoc />
        public Type WireType => typeof(DepartmentTree);

        /// <inheritdoc />
        public IReadOnlyList<string> WireMemberNames => s_wireMembers;

        /// <summary>
        /// Serializes the value.
        /// </summary>
        public void Serialize(ref MessagePackWriter writer, DepartmentTree? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(s_wireMembers.Length);

            writer.Write(nameof(DepartmentTree.CompanyId));
            MessagePackSerializer.Serialize<string>(ref writer, value.CompanyId, options);

            writer.Write(nameof(DepartmentTree.Roots));
            MessagePackSerializer.Serialize<DepartmentNodeCollection?>(ref writer, value.Roots, options);
        }

        /// <summary>
        /// Deserializes the value, skipping keys this version does not know.
        /// </summary>
        public DepartmentTree? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            options.Security.DepthStep(ref reader);
            try
            {
                string companyId = string.Empty;
                DepartmentNodeCollection? roots = null;

                var count = reader.ReadMapHeader();
                for (var i = 0; i < count; i++)
                {
                    switch (reader.ReadString())
                    {
                        case nameof(DepartmentTree.CompanyId):
                            companyId = MessagePackSerializer.Deserialize<string>(ref reader, options);
                            break;
                        case nameof(DepartmentTree.Roots):
                            roots = MessagePackSerializer.Deserialize<DepartmentNodeCollection?>(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                return new DepartmentTree { CompanyId = companyId, Roots = roots };
            }
            finally
            {
                reader.Depth--;
            }
        }
    }
}
