using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Conventions;
using Polhem.Business;
using Polhem.Definition.Attributes;
using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.UnitTests.Conventions
{
    /// <summary>
    /// Tests for POLHEM3001 (public BO methods must declare access control).
    /// </summary>
    public class BusinessObjectAccessControlAnalyzerTests
    {
        private static readonly Type[] s_anchors =
        {
            typeof(BusinessObject),
            typeof(ApiAccessControlAttribute),
        };

        private const string Preamble = """
            using System;
            using Polhem.Business;
            using Polhem.Definition;
            using Polhem.Definition.Attributes;
            """;

        [Fact]
        [DisplayName("A public BO method without access control reports POLHEM3001")]
        public void UnmarkedPublicMethod_ReportsDiagnostic()
        {
            var source = Preamble + """

                public class OrderBusinessObject : BusinessObject
                {
                    public OrderBusinessObject(IPolhemContext ctx, Guid accessToken)
                        : base(ctx, accessToken) { }

                    public string Approve(string id) => id;
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new BusinessObjectAccessControlAnalyzer(), source, s_anchors);

            // Assert
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM3001", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("'Approve'", message, StringComparison.Ordinal);
            Assert.Contains("UnauthorizedAccessException", message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A method marked itself reports nothing")]
        public void MethodLevelAttribute_ReportsNothing()
        {
            var source = Preamble + """

                public class OrderBusinessObject : BusinessObject
                {
                    public OrderBusinessObject(IPolhemContext ctx, Guid accessToken)
                        : base(ctx, accessToken) { }

                    [ApiAccessControl(ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated)]
                    public string Approve(string id) => id;
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new BusinessObjectAccessControlAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A type-level attribute covers all of its methods")]
        public void TypeLevelAttribute_CoversAllMethods()
        {
            var source = Preamble + """

                [ApiAccessControl(ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated)]
                public class OrderBusinessObject : BusinessObject
                {
                    public OrderBusinessObject(IPolhemContext ctx, Guid accessToken)
                        : base(ctx, accessToken) { }

                    public string Approve(string id) => id;

                    public string Reject(string id) => id;
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new BusinessObjectAccessControlAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("Constructors and properties are not API surface and report nothing")]
        public void ConstructorsAndProperties_AreNotReported()
        {
            var source = Preamble + """

                public class OrderBusinessObject : BusinessObject
                {
                    public OrderBusinessObject(IPolhemContext ctx, Guid accessToken)
                        : base(ctx, accessToken) { }

                    public string Label { get; set; } = string.Empty;
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new BusinessObjectAccessControlAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("Non-public methods are not API surface and report nothing")]
        public void NonPublicMethods_AreNotReported()
        {
            var source = Preamble + """

                public class OrderBusinessObject : BusinessObject
                {
                    public OrderBusinessObject(IPolhemContext ctx, Guid accessToken)
                        : base(ctx, accessToken) { }

                    protected string Prepare(string id) => id;

                    private string Normalise(string id) => id;
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new BusinessObjectAccessControlAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("A type that is not a BusinessObject is not subject to this rule")]
        public void NonBusinessObjectType_IsUnaffected()
        {
            var source = Preamble + """

                public class OrderService
                {
                    public string Approve(string id) => id;
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new BusinessObjectAccessControlAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }

        [Fact]
        [DisplayName("An override of a marked base method reports nothing")]
        public void OverrideOfMarkedBaseMethod_ReportsNothing()
        {
            var source = Preamble + """

                public class BaseOrderBusinessObject : BusinessObject
                {
                    public BaseOrderBusinessObject(IPolhemContext ctx, Guid accessToken)
                        : base(ctx, accessToken) { }

                    [ApiAccessControl(ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated)]
                    public virtual string Approve(string id) => id;
                }

                public class DerivedOrderBusinessObject : BaseOrderBusinessObject
                {
                    public DerivedOrderBusinessObject(IPolhemContext ctx, Guid accessToken)
                        : base(ctx, accessToken) { }

                    public override string Approve(string id) => id;
                }
                """;

            // Act
            var diagnostics = AnalyzerRunner.RunOnSource(
                new BusinessObjectAccessControlAnalyzer(), source, s_anchors);

            // Assert
            Assert.Empty(diagnostics);
        }
    }
}
