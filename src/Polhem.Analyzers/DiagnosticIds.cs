namespace Polhem.Analyzers
{
    /// <summary>
    /// Diagnostic identifiers reported by the Polhem convention analyzers.
    /// </summary>
    /// <remarks>
    /// The numeric ranges group rules by the kind of convention they enforce, which also
    /// corresponds to the analysis pipeline each one uses:
    /// <list type="bullet">
    ///   <item><description>POLHEM1xxx: single definition file validation, read via AdditionalFiles.</description></item>
    ///   <item><description>POLHEM2xxx: cross-file consistency between definition files.</description></item>
    ///   <item><description>POLHEM3xxx: C# coding conventions.</description></item>
    ///   <item><description>POLHEM4xxx: serialisation and wire contract rules.</description></item>
    /// </list>
    /// </remarks>
    internal static class DiagnosticIds
    {
        /// <summary>
        /// A FormSchema declares a CategoryId that is not a valid database scope.
        /// </summary>
        public const string InvalidFormSchemaCategoryId = "POLHEM1001";

        /// <summary>
        /// A DbCategorySettings category declares an identifier that is not a valid database scope.
        /// </summary>
        public const string InvalidDbCategoryId = "POLHEM1002";

        /// <summary>
        /// A field declares a data type that is not a member of the framework's field type enumeration.
        /// </summary>
        public const string InvalidFieldDbType = "POLHEM1003";

        /// <summary>
        /// A comma separated field list references a field the schema does not declare.
        /// </summary>
        public const string UnknownFieldListReference = "POLHEM1004";

        /// <summary>
        /// A relation field mapping targets a field the schema does not declare.
        /// </summary>
        public const string UnknownMappingDestinationField = "POLHEM1005";

        /// <summary>
        /// A field is marked as a relation field but no mapping populates it.
        /// </summary>
        public const string UnmappedRelationField = "POLHEM1006";

        /// <summary>
        /// A table declares the same field name more than once.
        /// </summary>
        public const string DuplicateFieldName = "POLHEM1007";

        /// <summary>
        /// A FormSchema table is not registered under the matching category in DbCategorySettings.
        /// </summary>
        public const string TableNotRegisteredInCategory = "POLHEM2001";

        /// <summary>
        /// A FormSchema table has no table schema under the folder matching its declared scope.
        /// </summary>
        public const string MissingTableSchema = "POLHEM2002";

        /// <summary>
        /// A relation field references a program identifier that no FormSchema declares.
        /// </summary>
        public const string UnknownRelationProgId = "POLHEM2003";

        /// <summary>
        /// A relation field mapping reads a field the referenced schema does not declare.
        /// </summary>
        public const string UnknownMappingSourceField = "POLHEM2004";

        /// <summary>
        /// A FormSchema has no corresponding FormLayout, which the run time requires.
        /// </summary>
        public const string MissingFormLayout = "POLHEM2005";

        /// <summary>
        /// A persisted FormSchema field is absent from the corresponding table schema.
        /// </summary>
        public const string FieldMissingFromTableSchema = "POLHEM2006";

        /// <summary>
        /// A language resource key is present in some cultures but missing from others.
        /// </summary>
        public const string InconsistentLanguageCoverage = "POLHEM2007";

        /// <summary>
        /// A public method on a business object declares no API access control.
        /// </summary>
        public const string MissingApiAccessControl = "POLHEM3001";

        /// <summary>
        /// A definition type exposes a collection property that is not a framework collection.
        /// </summary>
        public const string NonFrameworkCollectionProperty = "POLHEM3002";

        /// <summary>
        /// A public method on an ExecFunc handler declares no ExecFunc access control.
        /// </summary>
        public const string MissingExecFuncAccessControl = "POLHEM3003";

        /// <summary>
        /// A form schema declares no <c>PermissionModelId</c>, so its layer-1 authorization is a
        /// no-op and every authenticated caller may read and write through it.
        /// </summary>
        public const string MissingPermissionModelId = "POLHEM1008";

        /// <summary>
        /// Identifiers that are reserved and never reused: POLHEM4001 to POLHEM4004.
        /// </summary>
        /// <remarks>
        /// Bee.NET, which Polhem continues under a new name, shipped and then retired BEE4001 to
        /// BEE4004. A project migrated from Bee.NET maps its suppressions to POLHEM IDs by number, so a
        /// new rule under one of these numbers would be silently suppressed there. The documented rule
        /// list names them as reserved; <c>DiagnosticIdDocumentationTests</c> checks that none of them
        /// is declared as a rule.
        /// </remarks>
        public static readonly IReadOnlyList<string> ReservedIds = new[] { "POLHEM4001", "POLHEM4002", "POLHEM4003", "POLHEM4004" };

        /// <summary>
        /// A collection declares an additional public Add overload, which reflection-only
        /// serialization cannot resolve unambiguously.
        /// </summary>
        public const string AmbiguousCollectionAdd = "POLHEM4005";

        /// <summary>
        /// A serialized type has no parameterless constructor, so deserialization cannot create it.
        /// </summary>
        public const string MissingParameterlessConstructor = "POLHEM4006";
    }
}
