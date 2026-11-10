using System.ComponentModel;
using System.Globalization;
using Polhem.Analyzers.Conventions;
using Polhem.Repository;
using Microsoft.CodeAnalysis;

namespace Polhem.Analyzers.UnitTests.Conventions
{
    /// <summary>
    /// Tests for POLHEM3004 (a repository must create its database access through CreateDbAccess).
    /// </summary>
    public class RepositoryDbAccessFactoryAnalyzerTests
    {
        private static readonly Type[] s_anchors = [typeof(RepositoryBase), typeof(Polhem.Db.DbAccess)];

        private const string Preamble = """
            using System;
            using Polhem.Db;
            using Polhem.Definition;
            using Polhem.Repository;
            """;

        [Fact]
        [DisplayName("A repository that takes its database access from the context's factory reports POLHEM3004")]
        public void RepositoryUsingFactory_ReportsDiagnostic()
        {
            var source = Preamble + """

                public sealed class OrderReportRepository : RepositoryBase
                {
                    public OrderReportRepository(IRepositoryContext ctx, Guid token)
                        : base(ctx, token, "Order", DbScope.Company) { }

                    public DbAccess Open() => Context.DbAccessFactory.Create(DatabaseId);
                }
                """;

            var diagnostics = AnalyzerRunner.RunOnSource(new RepositoryDbAccessFactoryAnalyzer(), source, s_anchors);

            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("POLHEM3004", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("'OrderReportRepository'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A repository that uses CreateDbAccess reports nothing")]
        public void RepositoryUsingCreateDbAccess_ReportsNothing()
        {
            var source = Preamble + """

                public sealed class OrderReportRepository : RepositoryBase
                {
                    public OrderReportRepository(IRepositoryContext ctx, Guid token)
                        : base(ctx, token, "Order", DbScope.Company) { }

                    public DbAccess Open() => CreateDbAccess();
                }
                """;

            Assert.Empty(AnalyzerRunner.RunOnSource(new RepositoryDbAccessFactoryAnalyzer(), source, s_anchors));
        }

        [Theory]
        [InlineData("private readonly IDbAccessFactory _factory; public OrderReportRepository(IRepositoryContext ctx, Guid token, IDbAccessFactory factory) : base(ctx, token, \"Order\", DbScope.Company) { _factory = factory; } public DbAccess Open() => _factory.Create(DatabaseId);")]
        [InlineData("public OrderReportRepository(IRepositoryContext ctx, Guid token) : base(ctx, token, \"Order\", DbScope.Company) { } public DbAccess Open() => new DbAccess(DatabaseId, Context.ConnectionManager);")]
        [InlineData("public OrderReportRepository(IRepositoryContext ctx, Guid token) : base(ctx, token, \"Order\", DbScope.Company) { } private sealed class Reader { public DbAccess Open(IRepositoryContext ctx) => ctx.DbAccessFactory.Create(\"company\"); }")]
        [DisplayName("A repository that injects a factory, constructs a DbAccess, or reaches the factory from a nested type reports POLHEM3004")]
        public void RepositoryBypassingCreateDbAccess_ReportsDiagnostic(string members)
        {
            var source = Preamble + """

                public sealed class OrderReportRepository : RepositoryBase
                {
                """ + members + """

                }
                """;

            var diagnostic = Assert.Single(AnalyzerRunner.RunOnSource(new RepositoryDbAccessFactoryAnalyzer(), source, s_anchors));
            Assert.Equal("POLHEM3004", diagnostic.Id);
        }

        [Fact]
        [DisplayName("A class that is not a repository may use the context's factory")]
        public void NonRepositoryUsingFactory_ReportsNothing()
        {
            var source = Preamble + """

                public sealed class SchemaSeeder
                {
                    public DbAccess Open(IRepositoryContext ctx) => ctx.DbAccessFactory.Create("common");
                }
                """;

            Assert.Empty(AnalyzerRunner.RunOnSource(new RepositoryDbAccessFactoryAnalyzer(), source, s_anchors));
        }
    }
}
