using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Definitions;
using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.UnitTests.Definitions
{
    /// <summary>
    /// Tests for POLHEM1008 (a FormSchema without a PermissionModelId is open to every authenticated caller).
    /// </summary>
    /// <remarks>
    /// This rule **reports and does not enforce**: leaving unmarked forms open is the framework's deliberate gradual
    /// adoption strategy (see the XML doc of <c>FormBusinessObject.Authorize</c>). Enforcing it would break every
    /// deployment that is halfway through adoption. What was missing was never a rule, but any place that says
    /// "this form is open".
    /// </remarks>
    public class PermissionModelAnalyzerTests
    {
        private const string SchemaPath = "Define/FormSchema/Order.FormSchema.xml";

        [Fact]
        [DisplayName("A missing PermissionModelId reports POLHEM1008 with Info severity")]
        public void MissingPermissionModelId_ReportsInfoDiagnostic()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Order" CategoryId="company">
                  <Tables />
                </FormSchema>
                """;

            var diagnostics = AnalyzerRunner.Run(new PermissionModelAnalyzer(), (SchemaPath, xml));

            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM1008", diagnostic.Id);

            // Info rather than Warning is deliberate. Warning would fail the build of every deployment that has not
            // finished adopting, including this framework's own `Defaults/` (Department and Employee have no model).
            Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'Order'", message, StringComparison.Ordinal);
            Assert.Contains("every authenticated caller", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A blank PermissionModelId reports POLHEM1008 as well")]
        public void BlankPermissionModelId_ReportsDiagnostic()
        {
            // At runtime the check is `string.IsNullOrEmpty`, so an empty string is the same as a missing attribute.
            // Checking only whether the attribute is present would miss this shape.
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Order" CategoryId="company" PermissionModelId="   ">
                  <Tables />
                </FormSchema>
                """;

            var diagnostics = AnalyzerRunner.Run(new PermissionModelAnalyzer(), (SchemaPath, xml));

            Assert.Equal("POLHEM1008", Assert.Single(diagnostics).Id);
        }

        [Fact]
        [DisplayName("A declared PermissionModelId does not report POLHEM1008")]
        public void DeclaredPermissionModelId_ReportsNothing()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Order" CategoryId="company" PermissionModelId="OrderModel">
                  <Tables />
                </FormSchema>
                """;

            Assert.Empty(AnalyzerRunner.Run(new PermissionModelAnalyzer(), (SchemaPath, xml)));
        }

        [Fact]
        [DisplayName("Several forms each report once without swallowing one another")]
        public void MultipleSchemas_EachReportsOnce()
        {
            const string open1 = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Order" CategoryId="company"><Tables /></FormSchema>
                """;
            const string open2 = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Invoice" CategoryId="company"><Tables /></FormSchema>
                """;
            const string guarded = """
                <?xml version="1.0" encoding="utf-8"?>
                <FormSchema ProgId="Payment" CategoryId="company" PermissionModelId="PayModel"><Tables /></FormSchema>
                """;

            var diagnostics = AnalyzerRunner.Run(
                new PermissionModelAnalyzer(),
                ("Define/FormSchema/Order.FormSchema.xml", open1),
                ("Define/FormSchema/Invoice.FormSchema.xml", open2),
                ("Define/FormSchema/Payment.FormSchema.xml", guarded));

            Assert.Equal(2, diagnostics.Length);
            Assert.All(diagnostics, d => Assert.Equal("POLHEM1008", d.Id));
        }
    }
}
