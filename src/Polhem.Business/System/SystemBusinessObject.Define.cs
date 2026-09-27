using Polhem.Base.Serialization;
using Polhem.Base.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Attributes;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Organization;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Business.System
{
    /// <summary>
    /// Definition-access half of <see cref="SystemBusinessObject"/> (get / save define, form schema,
    /// layout, language and department tree). Split out for file size only; behaviour is unchanged.
    /// </summary>
    public partial class SystemBusinessObject
    {
        /// <summary>
        /// Core method for retrieving definition data.
        /// </summary>
        /// <param name="args">The input arguments.</param>
        private GetDefineResult GetDefineCore(GetDefineArgs args)
        {
            var result = new GetDefineResult();
            object value = args.DefineType switch
            {
                // The menu is the one definition served here that carries a tenant overlay, and the
                // overlay is whole-file, so the resolved menu is returned rather than the two layers.
                // The customization code comes from the session, never from the arguments — accepting
                // it from the caller would let anyone read any tenant's menu.
                DefineType.MenuSettings => DefineAccess.GetMenuSettings(GetCurrentCustomizeId()),
                DefineType.DatabaseSettings => ReadDatabaseSettingsAsStored(),
                _ => DefineAccess.GetDefine(args.DefineType, args.Keys),
            };

            if (value != null)
            {
                // Serialize the object to XML
                result.Xml = SerializeDefine(value);
            }

            return result;
        }

        /// <summary>
        /// Reads <c>DatabaseSettings.xml</c> from disk, leaving passwords in their <c>enc:</c> form.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This API hands back a definition <b>as stored</b>, and for this one type the cached
        /// instance cannot honour that: <c>GetDatabaseSettings</c> decrypts in
        /// place on first read, so the cache holds plain-text passwords from then on. Serving that
        /// instance would put credentials in the response.
        /// </para>
        /// <para>
        /// WARNING: the bypass belongs here, at the API boundary, and not in
        /// <c>CacheDefineAccess</c>. Its <c>GetDefine</c> is the framework's general definition
        /// accessor — several <see cref="IDefineAccess"/> members route through it — so special-casing a
        /// type there would change what every internal caller receives. Worse, a class named for
        /// caching that quietly skips the cache for one type is a trap for whoever reads it next.
        /// </para>
        /// <para>
        /// Callers needing usable credentials — building a connection string, testing a
        /// connection — go through <see cref="IDefineAccess.GetDatabaseSettings"/>, which is cached and
        /// decrypted and is untouched by this. Reading the file here is affordable because this
        /// path is local-only tooling asking for the definition, not a per-request lookup.
        /// </para>
        /// </remarks>
        private DatabaseSettings ReadDatabaseSettingsAsStored()
        {
            var paths = Services.GetRequiredService<PathOptions>();
            string filePath = paths.GetDatabaseSettingsFilePath();
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The file {filePath} does not exist.");

            return XmlCodec.DeserializeFromFile<DatabaseSettings>(filePath)!;
        }

        /// <summary>
        /// Gets definition data. A remote caller may read only the definition types a client needs to
        /// render forms and menus; a local call may read every type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The remote allow-list is <see cref="DefineType.FormSchema"/>, <see cref="DefineType.FormLayout"/>,
        /// <see cref="DefineType.Language"/>, <see cref="DefineType.MenuSettings"/>,
        /// <see cref="DefineType.CurrencySettings"/> and <see cref="DefineType.UnitSettings"/>: the types
        /// the shipped clients request. Every other type — the server settings, the type registry,
        /// table schemas, database categories, permission models, plugin bindings, and any type added
        /// later — is refused to a remote caller until it is added to the list on purpose.
        /// </para>
        /// <para>
        /// An allow-list rather than a deny-list because a deny-list fails open: each new definition
        /// type used to become remotely readable the day it was added.
        /// </para>
        /// </remarks>
        /// <param name="args">The input arguments.</param>
        /// <exception cref="UserMessageException">A remote caller asked for a type outside the allow-list.</exception>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public virtual GetDefineResult GetDefine(GetDefineArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            if (!IsLocalCall && !IsRemoteReadableDefine(args.DefineType))
                throw new UserMessageException("The specified DefineType is not supported.");
            return GetDefineCore(args);
        }

        /// <summary>
        /// Returns whether a remote caller may read the definition type.
        /// </summary>
        /// <param name="defineType">The definition type in question.</param>
        private static bool IsRemoteReadableDefine(DefineType defineType)
            => defineType is DefineType.FormSchema
                          or DefineType.FormLayout
                          or DefineType.Language
                          or DefineType.MenuSettings
                          or DefineType.CurrencySettings
                          or DefineType.UnitSettings;

        /// <summary>
        /// Returns the raw <see cref="FormSchema"/> definition as XML.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The per-type entry point for ordinary clients, as against <see cref="GetDefine"/>, which
        /// serves every definition type and is gated for tooling. Both carry XML and both serve the
        /// definition <b>as stored</b>: no localization, no number-format baking, no customization
        /// overlay. Callers apply those themselves — <see cref="FormSchemaLocalizer"/>,
        /// <see cref="Polhem.Definition.Forms.NumberFormatApplier"/> and <see cref="Polhem.Definition.Customization.CustomizeOverlay"/> all live in <c>Polhem.Definition</c>
        /// so the server and every client run the identical code.
        /// </para>
        /// <para>
        /// XML rather than a JSON tree because definition types declare XML as their serialisation
        /// contract: their nested collections are get-only, which XmlSerializer handles by
        /// populating the existing instance, while JSON and MessagePack bind by writability and
        /// would silently drop those collections on the way back.
        /// </para>
        /// </remarks>
        /// <param name="args">The input arguments carrying the target <c>ProgId</c>.</param>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public virtual GetFormSchemaResult GetFormSchema(GetFormSchemaArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            if (string.IsNullOrWhiteSpace(args.ProgId))
                throw new UserMessageException("ProgId is required.");

            var schema = DefineAccess.GetDefine(DefineType.FormSchema, new[] { args.ProgId }) as FormSchema
                ?? throw new UserMessageException($"FormSchema '{args.ProgId}' not found.");
            return new GetFormSchemaResult { Xml = SerializeDefine(schema) };
        }

        /// <summary>
        /// Returns the current company's department tree (per-company organisation hierarchy),
        /// scoped to the session's company. JSON-friendly for JS frontends; the tree is
        /// <c>null</c> when no company has been entered.
        /// </summary>
        /// <param name="args">The input arguments (carries no fields).</param>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public virtual GetDepartmentTreeResult GetDepartmentTree(GetDepartmentTreeArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);

            var sessionInfo = SessionInfoService.Get(AccessToken)
                ?? throw new AuthenticationRequiredException("Session not found or has expired.");

            DepartmentTree? tree = null;
            if (!string.IsNullOrEmpty(sessionInfo.CompanyId))
            {
                tree = Services.GetRequiredService<IDepartmentTreeService>().Get(sessionInfo.CompanyId);
            }
            return new GetDepartmentTreeResult { Tree = tree };
        }

        /// <summary>
        /// Returns the raw base-layer <see cref="FormLayout"/> definition as XML, or an empty
        /// string when no layout is stored for that identifier.
        /// </summary>
        /// <remarks>
        /// Serves the definition as stored, and the customization layer is a separate call. A caller
        /// assembles the runtime layout itself: fetch this and the customization layout, pick
        /// between them with <see cref="Polhem.Definition.Customization.CustomizeOverlay"/>, and take the captions from the localized
        /// schema. Layouts are authored at design time, so an empty result from both layers is a
        /// configuration error for the caller to report — not a cue to generate one from the
        /// <see cref="FormSchema"/>.
        /// </remarks>
        /// <param name="args">
        /// The input arguments. <c>ProgId</c> is required; an empty <c>LayoutId</c> resolves to
        /// <c>ProgId</c>, matching the <c>{ProgId}.FormLayout.xml</c> file convention.
        /// </param>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public virtual GetFormLayoutResult GetFormLayout(GetFormLayoutArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            if (string.IsNullOrWhiteSpace(args.ProgId))
                throw new UserMessageException("ProgId is required.");

            // An empty LayoutId means "this form's own layout". It resolves to the ProgId rather
            // than a literal "default" because layout definition files are named after the progId
            // ({ProgId}.FormLayout.xml, LayoutId == ProgId); "default" would never match a file.
            var layoutId = string.IsNullOrWhiteSpace(args.LayoutId) ? args.ProgId : args.LayoutId;

            // Base layer only. The caller fetches the customization layer through
            // GetCustomizeFormLayout and picks between them with CustomizeOverlay; when neither
            // layer has a definition that is a configuration error, not a cue to generate one.
            var layout = DefineAccess.FindFormLayout(string.Empty, layoutId);
            return new GetFormLayoutResult { Xml = layout is null ? string.Empty : SerializeDefine(layout) };
        }

        /// <summary>
        /// Returns the raw <see cref="LanguageResource"/> definition as XML, or an empty string when no
        /// resource is stored for that language and namespace.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Serves the resource as stored, like <see cref="GetFormSchema"/> and <see cref="GetFormLayout"/>;
        /// the XML is the same that <see cref="GetDefine"/> returns for <see cref="DefineType.Language"/>.
        /// A missing resource is a normal scenario (a translation not written yet), not an error.
        /// </para>
        /// <para>
        /// The resource is read from the Define cache via
        /// <see cref="IDefineAccess.GetLanguage"/>. Per
        /// <c>docs/en/development-constraints.md § Cached Data Immutability After Init</c>,
        /// the cached instance must not be mutated; it is only serialized here.
        /// </para>
        /// </remarks>
        /// <param name="args">The input arguments carrying <c>Lang</c> and <c>Namespace</c>.</param>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public virtual GetLanguageResult GetLanguage(GetLanguageArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            if (string.IsNullOrWhiteSpace(args.Lang))
                throw new UserMessageException("Lang is required.");
            if (string.IsNullOrWhiteSpace(args.Namespace))
                throw new UserMessageException("Namespace is required.");

            // `GetLanguage` returns null when the resource file does not exist. That is a normal
            // scenario (a missing translation), not an error.
            var resource = DefineAccess.GetLanguage(args.Lang, args.Namespace);
            return new GetLanguageResult { Xml = resource is null ? string.Empty : SerializeDefine(resource) };
        }

        /// <summary>
        /// Returns the tenant customization layer of a form layout definition as XML, or an empty
        /// string when this session's tenant supplies no override.
        /// </summary>
        /// <remarks>
        /// The companion of <see cref="GetFormLayout"/>: a caller fetches both layers and picks
        /// between them with <see cref="Polhem.Definition.Customization.CustomizeOverlay"/>. Kept as a separate call rather than a second
        /// field on one response so the connector's existing method contracts stay as they are.
        /// <para>
        /// <b>Which tenant is not negotiable.</b> The customization code comes from
        /// <see cref="Polhem.Definition.Identity.SessionInfo.CustomizeId"/> and is deliberately absent from the arguments — accepting
        /// it from the caller would let anyone read any tenant's customization.
        /// </para>
        /// </remarks>
        /// <param name="args">The input arguments. <c>ProgId</c> is required; an empty <c>LayoutId</c> resolves to <c>ProgId</c>.</param>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public virtual GetFormLayoutResult GetCustomizeFormLayout(GetFormLayoutArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            if (string.IsNullOrWhiteSpace(args.ProgId))
                throw new UserMessageException("ProgId is required.");

            string customizeId = GetCurrentCustomizeId();
            if (string.IsNullOrEmpty(customizeId))
                return new GetFormLayoutResult { Xml = string.Empty };

            var layoutId = string.IsNullOrWhiteSpace(args.LayoutId) ? args.ProgId : args.LayoutId;
            var layout = Services.GetRequiredService<ICustomizeDefineReader>()
                .GetCustomizeFormLayout(customizeId, layoutId);
            return new GetFormLayoutResult { Xml = layout is null ? string.Empty : SerializeDefine(layout) };
        }

        /// <summary>
        /// Returns the tenant customization layer of a language resource as XML, or an empty string
        /// when this session's tenant supplies no override.
        /// </summary>
        /// <remarks>
        /// The companion of <see cref="GetLanguage"/>; see
        /// <see cref="GetCustomizeFormLayout"/> for why the customization code is taken from the
        /// session rather than the arguments.
        /// </remarks>
        /// <param name="args">The input arguments carrying <c>Lang</c> and <c>Namespace</c>.</param>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public virtual GetLanguageResult GetCustomizeLanguage(GetLanguageArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            if (string.IsNullOrWhiteSpace(args.Lang))
                throw new UserMessageException("Lang is required.");
            if (string.IsNullOrWhiteSpace(args.Namespace))
                throw new UserMessageException("Namespace is required.");

            string customizeId = GetCurrentCustomizeId();
            if (string.IsNullOrEmpty(customizeId))
                return new GetLanguageResult { Xml = string.Empty };

            var resource = Services.GetRequiredService<ICustomizeDefineReader>()
                .GetCustomizeLanguage(customizeId, args.Lang, args.Namespace);
            return new GetLanguageResult { Xml = resource is null ? string.Empty : SerializeDefine(resource) };
        }

        /// <summary>
        /// Serializes a definition for the wire.
        /// </summary>
        /// <param name="define">The definition object.</param>
        /// <remarks>
        /// Most definitions handed here come from the process-wide cache and are serialized as they
        /// are, without a copy. That is safe because <see cref="XmlCodec.Serialize"/> sets no state on
        /// the value; empty collections are omitted by the definitions' own
        /// <c>{Property}Specified</c> properties.
        /// <para>
        /// <see cref="DefineType.DatabaseSettings"/> is the one that must not be served from the
        /// cache at all — see <c>GetDefine</c>, which reads it from file so the
        /// passwords stay encrypted.
        /// </para>
        /// </remarks>
        private static string SerializeDefine(object define)
        {
            return XmlCodec.Serialize(define);
        }

        /// <summary>
        /// Core method for saving definition data.
        /// </summary>
        /// <param name="args">The input arguments.</param>
        private SaveDefineResult SaveDefineCore(SaveDefineArgs args)
        {
            // Deserialize XML to the target object
            var type = args.DefineType.ToClrType();
            object? defineObject = XmlCodec.Deserialize(args.Xml, type);
            if (defineObject == null)
                throw new InvalidOperationException($"Failed to deserialize XML to {type.Name} object.");

            // Save the definition data
            DefineAccess.SaveDefine(args.DefineType, defineObject, args.Keys);
            var result = new SaveDefineResult();
            return result;
        }

        /// <summary>
        /// Saves definition data. Restricted to local calls.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Writing a definition is a deployment-time operation, not an application one, so this
        /// method is <see cref="ApiProtectionLevel.LocalOnly"/> — stricter than any token- or
        /// permission-based check a remote caller could satisfy. Tooling that maintains
        /// definitions (the define editor, deployment scripts) runs against a local connection;
        /// remote clients read definitions through <see cref="GetDefine"/> and never write them.
        /// </para>
        /// <para>
        /// The previous guard only rejected <see cref="SystemSettings"/> and <see cref="DatabaseSettings"/> from
        /// remote callers, which left every other definition type writable by any authenticated
        /// account — including <see cref="PermissionModels"/> (the authorisation model itself),
        /// <see cref="DbCategorySettings"/> (which database each table resolves to) and <see cref="FormSchema"/>
        /// (whose expressions are evaluated server-side). Gating the whole method removes that
        /// class of escalation rather than enumerating the sensitive types.
        /// </para>
        /// </remarks>
        /// <param name="args">The input arguments.</param>
        [ApiAccessControl(ApiProtectionLevel.LocalOnly, ApiAccessRequirement.Authenticated)]
        public virtual SaveDefineResult SaveDefine(SaveDefineArgs args)
        {
            // Defence in depth, deliberately kept alongside the LocalOnly attribute rather than
            // relying on it alone: ApiAccessValidator only runs on the JSON-RPC dispatch path, so a
            // caller that constructs the BO directly — in-process hosting, a custom dispatcher, a
            // subclass — never passes through it. The attribute stops remote API traffic; this stops
            // everything else.
            if (!IsLocalCall)
                throw new NotSupportedException("SaveDefine is restricted to local calls.");

            return SaveDefineCore(args);
        }
    }
}
