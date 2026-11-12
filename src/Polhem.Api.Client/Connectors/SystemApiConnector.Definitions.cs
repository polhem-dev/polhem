using Polhem.Core;
using Polhem.Core.Serialization;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Language;
using Polhem.Definition.Organization;

namespace Polhem.Api.Client.Connectors
{
    /// <summary>
    /// Definition calls of <see cref="SystemApiConnector"/>: reading definitions, both layers of the tenant
    /// customization, and saving a definition. Split out for file size only; behaviour is unchanged.
    /// </summary>
    public partial class SystemApiConnector
    {
        /// <summary>
        /// Asynchronously gets definition data.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="defineType">The definition data type.</param>
        /// <param name="keys">The keys used to locate the definition data.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null,
            CancellationToken cancellationToken = default)
        {
            var request = new GetDefineRequest()
            {
                DefineType = defineType,
                Keys = keys
            };
            var result = await ExecuteAsync<GetDefineResponse>(SystemActions.GetDefine, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (StringUtilities.IsNotEmpty(result.Xml))
                return XmlCodec.Deserialize<T>(result.Xml)!;
            else
                return default!;
        }

        /// <summary>
        /// Asynchronously gets the tenant customization layer of a form layout definition;
        /// <c>null</c> when this session's tenant supplies no override.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="layoutId">The layout identifier; empty resolves to <paramref name="progId"/>.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// The companion of the base-layer fetch: obtain both layers and pick between them with
        /// <see cref="Polhem.Definition.Customization.CustomizeOverlay"/>. There is deliberately no customization-code parameter — the
        /// server takes it from the session, so a caller cannot ask for another tenant's overrides.
        /// </remarks>
        public virtual async Task<FormLayout?> GetCustomizeFormLayoutAsync(string progId, string layoutId = "",
            CancellationToken cancellationToken = default)
        {
            var request = new GetFormLayoutRequest { ProgId = progId, LayoutId = layoutId };
            var result = await ExecuteAsync<GetFormLayoutResponse>(SystemActions.GetCustomizeFormLayout, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return StringUtilities.IsNotEmpty(result.Xml) ? XmlCodec.Deserialize<FormLayout>(result.Xml!) : null;
        }

        /// <summary>
        /// Asynchronously gets the tenant customization layer of a language resource;
        /// <c>null</c> when this session's tenant supplies no override.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="ns">The resource namespace.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <remarks>
        /// See <see cref="GetCustomizeFormLayoutAsync"/> for why there is no customization-code parameter.
        /// </remarks>
        public virtual async Task<LanguageResource?> GetCustomizeLanguageAsync(string lang, string ns,
            CancellationToken cancellationToken = default)
        {
            var request = new GetLanguageRequest { Lang = lang, Namespace = ns };
            var result = await ExecuteAsync<GetLanguageResponse>(SystemActions.GetCustomizeLanguage, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return StringUtilities.IsNotEmpty(result.Xml) ? XmlCodec.Deserialize<LanguageResource>(result.Xml!) : null;
        }

        /// <summary>
        /// Asynchronously gets the base form schema of a program, exactly as stored.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The schema, or <c>null</c> when the server has none for <paramref name="progId"/>.</returns>
        /// <remarks>
        /// <para>
        /// The per-type counterpart of <see cref="GetDefineAsync{T}"/> with
        /// <see cref="DefineType.FormSchema"/>. No localization or number formats are applied; a UI
        /// assembles those through <see cref="Polhem.Api.Client.Definitions.FormDefinitionLoader"/>.
        /// </para>
        /// <para>
        /// Definition types travel as XML rather than a JSON tree because they declare XML as their
        /// serialization contract: their nested collections are get-only, which XmlSerializer fills
        /// in place, while JSON and MessagePack bind by writability and would drop them on the way
        /// back without an error.
        /// </para>
        /// </remarks>
        public virtual async Task<FormSchema?> GetFormSchemaAsync(string progId, CancellationToken cancellationToken = default)
        {
            var request = new GetFormSchemaRequest { ProgId = progId };
            var result = await ExecuteAsync<GetFormSchemaResponse>(SystemActions.GetFormSchema, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return StringUtilities.IsNotEmpty(result.Xml) ? XmlCodec.Deserialize<FormSchema>(result.Xml!) : null;
        }

        /// <summary>
        /// Asynchronously gets the base layer of a form layout definition, exactly as stored.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="layoutId">The layout identifier; empty resolves to <paramref name="progId"/>.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The layout, or <c>null</c> when the base layer stores none.</returns>
        /// <remarks>
        /// The tenant's layer comes from <see cref="GetCustomizeFormLayoutAsync"/>; pick between the
        /// two with <see cref="Polhem.Definition.Customization.CustomizeOverlay"/>.
        /// </remarks>
        public virtual async Task<FormLayout?> GetFormLayoutAsync(string progId, string layoutId = "",
            CancellationToken cancellationToken = default)
        {
            var request = new GetFormLayoutRequest { ProgId = progId, LayoutId = layoutId };
            var result = await ExecuteAsync<GetFormLayoutResponse>(SystemActions.GetFormLayout, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return StringUtilities.IsNotEmpty(result.Xml) ? XmlCodec.Deserialize<FormLayout>(result.Xml!) : null;
        }

        /// <summary>
        /// Asynchronously gets the base layer of a language resource, exactly as stored.
        /// </summary>
        /// <param name="lang">The BCP-47 language code.</param>
        /// <param name="ns">The resource namespace.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The resource, or <c>null</c> when the base layer stores none.</returns>
        /// <remarks>
        /// The tenant's layer comes from <see cref="GetCustomizeLanguageAsync"/>.
        /// </remarks>
        public virtual async Task<LanguageResource?> GetLanguageAsync(string lang, string ns,
            CancellationToken cancellationToken = default)
        {
            var request = new GetLanguageRequest { Lang = lang, Namespace = ns };
            var result = await ExecuteAsync<GetLanguageResponse>(SystemActions.GetLanguage, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return StringUtilities.IsNotEmpty(result.Xml) ? XmlCodec.Deserialize<LanguageResource>(result.Xml!) : null;
        }

        /// <summary>
        /// Asynchronously gets the current company's department tree (per-company organisation
        /// hierarchy). JSON-friendly for frontends; returns <c>null</c> when no company is entered.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<DepartmentTree?> GetDepartmentTreeAsync(CancellationToken cancellationToken = default)
        {
            var request = new GetDepartmentTreeRequest();
            var result = await ExecuteAsync<GetDepartmentTreeResponse>(SystemActions.GetDepartmentTree, request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return result.Tree;
        }

        /// <summary>
        /// Asynchronously saves definition data.
        /// </summary>
        /// <param name="defineType">The definition data type.</param>
        /// <param name="defineObject">The definition data object.</param>
        /// <param name="keys">The keys used to locate where the definition data is saved.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<SaveDefineResponse> SaveDefineAsync(DefineType defineType, object defineObject, string[]? keys = null,
            CancellationToken cancellationToken = default)
        {
            var request = new SaveDefineRequest()
            {
                DefineType = defineType,
                Xml = XmlCodec.Serialize(defineObject),
                Keys = keys
            };
            return await ExecuteAsync<SaveDefineResponse>(SystemActions.SaveDefine, request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
