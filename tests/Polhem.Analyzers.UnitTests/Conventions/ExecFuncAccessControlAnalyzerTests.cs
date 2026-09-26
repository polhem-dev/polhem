using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Conventions;
using Polhem.Business;
using Polhem.Business.Attributes;
using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.UnitTests.Conventions
{
    /// <summary>
    /// Tests for POLHEM3003 (public ExecFunc handler methods must declare access control).
    /// </summary>
    public class ExecFuncAccessControlAnalyzerTests
    {
        private static readonly Type[] s_anchors =
        {
            typeof(IExecFuncHandler),
            typeof(ExecFuncAccessControlAttribute),
        };

        private const string Preamble = """
            using System;
            using Polhem.Business;
            using Polhem.Business.Attributes;
            using Polhem.Definition.Security;
            """;

        [Fact]
        [DisplayName("A public ExecFunc handler method without access control reports POLHEM3003")]
        public void UnmarkedHandlerMethod_ReportsDiagnostic()
        {
            var source = Preamble + """

                public class MaintenanceExecFuncHandler : IExecFuncHandler
                {
                    public void RebuildIndexes(ExecFuncArgs args, ExecFuncResult result) { }
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new ExecFuncAccessControlAnalyzer(), source, s_anchors);

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM3003", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'RebuildIndexes'", message, StringComparison.Ordinal);
            Assert.Contains("LocalOnly", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A method marked with ExecFuncAccessControl reports nothing")]
        public void MarkedHandlerMethod_ReportsNothing()
        {
            var source = Preamble + """

                public class MaintenanceExecFuncHandler : IExecFuncHandler
                {
                    [ExecFuncAccessControl(ApiAccessRequirement.Authenticated, LocalOnly = true)]
                    public void RebuildIndexes(ExecFuncArgs args, ExecFuncResult result) { }
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new ExecFuncAccessControlAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A static ExecFunc method is subject to the rule as well")]
        public void StaticHandlerMethod_IsReported()
        {
            var source = Preamble + """

                public class MaintenanceExecFuncHandler : IExecFuncHandler
                {
                    public static void Ping(ExecFuncArgs args, ExecFuncResult result) { }
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new ExecFuncAccessControlAnalyzer(), source, s_anchors);

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM3003", diagnostic.Id);
            Assert.Contains("'Ping'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A public method whose signature does not match the ExecFunc shape reports nothing")]
        public void NonMatchingSignature_ReportsNothing()
        {
            var source = Preamble + """

                public class MaintenanceExecFuncHandler : IExecFuncHandler
                {
                    public string Describe(string id) => id;

                    public void Reset() { }

                    public string Label { get; set; } = string.Empty;
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new ExecFuncAccessControlAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A non-public method of the ExecFunc shape cannot be dispatched and reports nothing")]
        public void NonPublicMethods_AreNotReported()
        {
            var source = Preamble + """

                public class MaintenanceExecFuncHandler : IExecFuncHandler
                {
                    protected void Prepare(ExecFuncArgs args, ExecFuncResult result) { }

                    private void Normalise(ExecFuncArgs args, ExecFuncResult result) { }
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new ExecFuncAccessControlAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A type that does not implement IExecFuncHandler is not subject to this rule")]
        public void NonHandlerType_IsUnaffected()
        {
            var source = Preamble + """

                public class MaintenanceService
                {
                    public void RebuildIndexes(ExecFuncArgs args, ExecFuncResult result) { }
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new ExecFuncAccessControlAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
