using System.Collections.Immutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Polhem.Analyzers.Definitions
{
    /// <summary>
    /// Reports POLHEM2008: a relation mapping whose destination field declares a data type that holds its values
    /// differently from the source field it is copied from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A relation field holds a value read from the referenced schema's master table, but the framework treats it by
    /// the type the destination declares. A filter on it converts its string value by that type
    /// (<c>FilterValueCoercion</c> in Polhem.Db), so a <c>Guid</c> source column declared as <c>String</c> on the
    /// relation field compares text with a uniqueidentifier, which matches nothing or is refused by the database.
    /// </para>
    /// <para>
    /// Types are compared by how the framework holds their values, not by name: <c>Integer</c> and
    /// <c>AutoIncrement</c> are both a 32-bit integer, <c>Decimal</c> and <c>Currency</c> both a decimal, and
    /// <c>String</c> and <c>Text</c> both text. An integer destination wider than its source (<c>Short</c> into
    /// <c>Integer</c> or <c>Long</c>, <c>Integer</c> into <c>Long</c>) holds every source value, and the database
    /// compares the two widths, so it is accepted; a narrower one is not.
    /// </para>
    /// <para>
    /// Only the mappings the query join reads are checked. A <c>LookupOnly</c> mapping copies a value into a field of
    /// this table while a lookup selection is edited, converting it by the destination's type, and a filter on that
    /// field reads this table's own column. A mapping the analyzer cannot resolve is left to the rules that report it
    /// (POLHEM1003, POLHEM1005, POLHEM2003, POLHEM2004), and a framework-supplied schema is not a file it can read.
    /// </para>
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RelationFieldTypeAnalyzer : DefinitionFileAnalyzer
    {
        private const string DefaultDbType = "String";

        private static readonly DiagnosticDescriptor TypeMismatch = new DiagnosticDescriptor(
            id: DiagnosticIds.RelationFieldTypeMismatch,
            title: "Relation field must declare the type of its source field",
            messageFormat: "Field '{1}' in FormSchema '{0}' declares DbType '{2}', but it is mapped from '{3}' in "
                         + "'{4}', which declares '{5}'. A filter on '{1}' converts its value as '{2}' and does not "
                         + "match the column. Fix: declare DbType=\"{5}\" on '{1}'.",
            category: "Polhem.Definition",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "A relation field shows a column of the referenced schema, and filters and sorts on it are "
                       + "run against that column. Declared with a different type, the field's values are converted "
                       + "wrongly and the filter matches nothing or fails.",
            helpLinkUri: null,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(TypeMismatch);

        /// <inheritdoc />
        protected override void Analyze(CompilationAnalysisContext context)
        {
            var definitions = DefinitionContext.Create(context.Options.AdditionalFiles, context.CancellationToken);

            foreach (var schema in definitions.FormSchemas)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                foreach (var declaration in schema.Fields)
                {
                    var relationProgId = declaration.Attribute("RelationProgId")?.Value;
                    if (string.IsNullOrEmpty(relationProgId) || FrameworkProgIds.IsFrameworkSupplied(relationProgId!))
                        continue;

                    var referenced = definitions.FindByProgId(relationProgId!);
                    var sourceFields = referenced is null ? null : MasterTableFields(referenced);
                    var destinationFields = FieldsOf(declaration.Ancestors("FormTable").FirstOrDefault());
                    if (sourceFields is null || destinationFields is null)
                        continue;

                    foreach (var mapping in declaration.Elements("RelationFieldMappings").Elements("FieldMapping"))
                        Check(context, schema, referenced!, mapping, sourceFields, destinationFields);
                }
            }
        }

        private static void Check(
            CompilationAnalysisContext context,
            FormSchemaModel schema,
            FormSchemaModel referenced,
            XElement mapping,
            Dictionary<string, XElement> sourceFields,
            Dictionary<string, XElement> destinationFields)
        {
            var sourceName = mapping.Attribute("SourceField")?.Value;
            var destinationName = mapping.Attribute("DestinationField")?.Value;
            if (string.IsNullOrEmpty(sourceName) || string.IsNullOrEmpty(destinationName) || IsLookupOnly(mapping))
                return;

            if (!sourceFields.TryGetValue(sourceName!, out var source)
                || !destinationFields.TryGetValue(destinationName!, out var destination))
                return;

            var sourceType = DbTypeOf(source);
            var destinationType = DbTypeOf(destination);
            if (!FieldDbTypes.IsValid(sourceType) || !FieldDbTypes.IsValid(destinationType))
                return;

            if (Holds(destinationType, sourceType))
                return;

            var location = destination.Attribute("DbType") ?? destination.Attribute("FieldName");
            if (location is null)
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                TypeMismatch,
                schema.CreateLocation(location),
                schema.ProgId,
                destination.Attribute("FieldName")?.Value ?? destinationName,
                destinationType,
                source.Attribute("FieldName")?.Value ?? sourceName,
                referenced.ProgId,
                sourceType));
        }

        // The framework reads a relation's source fields from the master table, the one named after the ProgId.
        private static Dictionary<string, XElement>? MasterTableFields(FormSchemaModel schema)
            => FieldsOf(schema.Tables.FirstOrDefault(table =>
                string.Equals(table.Attribute("TableName")?.Value, schema.ProgId, StringComparison.OrdinalIgnoreCase)));

        private static Dictionary<string, XElement>? FieldsOf(XElement? table)
        {
            if (table is null)
                return null;

            var fields = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var formField in table.Descendants("FormField"))
            {
                var name = formField.Attribute("FieldName")?.Value;
                if (!string.IsNullOrEmpty(name) && !fields.ContainsKey(name!))
                    fields.Add(name!, formField);
            }

            return fields;
        }

        // An absent attribute deserializes to the property's initial value, `FieldDbType.String`.
        private static string DbTypeOf(XElement formField)
            => formField.Attribute("DbType")?.Value ?? DefaultDbType;

        // Matches how `XmlSerializer` reads an `xs:boolean`: "true" and "1" are true, case-sensitively.
        private static bool IsLookupOnly(XElement mapping)
            => mapping.Attribute("LookupOnly")?.Value is "true" or "1";

        private static bool Holds(string destinationType, string sourceType)
        {
            var destination = ValueKind(destinationType);
            var source = ValueKind(sourceType);
            if (string.Equals(destination, source, StringComparison.Ordinal))
                return true;

            var destinationWidth = IntegerWidth(destination);
            var sourceWidth = IntegerWidth(source);
            return destinationWidth > 0 && sourceWidth > 0 && destinationWidth >= sourceWidth;
        }

        private static int IntegerWidth(string valueKind) => valueKind switch
        {
            "Short" => 16,
            "Integer" => 32,
            "Long" => 64,
            _ => 0,
        };

        private static string ValueKind(string dbType) => dbType switch
        {
            "AutoIncrement" => "Integer",
            "Currency" => "Decimal",
            "Text" => "String",
            _ => dbType,
        };
    }
}
