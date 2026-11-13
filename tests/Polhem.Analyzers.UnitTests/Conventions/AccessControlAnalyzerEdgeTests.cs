using System.ComponentModel;
using Polhem.Analyzers.Conventions;
using Polhem.Business;
using Polhem.Business.Attributes;
using Polhem.Definition.Attributes;

namespace Polhem.Analyzers.UnitTests.Conventions
{
    /// <summary>
    /// Edge cases of the access-control rules POLHEM3001 (BO methods) and POLHEM3003 (ExecFunc handler methods):
    /// compilations without the framework types, interfaces, half-matching signatures and unmarked override chains.
    /// </summary>
    public class AccessControlAnalyzerEdgeTests
    {
        private static readonly Type[] s_businessObjectAnchors =
        {
            typeof(BusinessObject),
            typeof(ApiAccessControlAttribute),
        };

        private static readonly Type[] s_execFuncAnchors =
        {
            typeof(IExecFuncHandler),
            typeof(ExecFuncAccessControlAttribute),
        };

        [Fact]
        [DisplayName("POLHEM3001 and POLHEM3003 stay silent in a compilation that does not reference the business layer")]
        public void Rules_WithoutBusinessLayer_StaySilent()
        {
            const string source = """
                public class OrderBusinessObject
                {
                    public string Approve(string id) => id;
                }
                """;

            Assert.Empty(AnalyzerRunner.Run(new BusinessObjectAccessControlAnalyzer(), source));
            Assert.Empty(AnalyzerRunner.Run(new ExecFuncAccessControlAnalyzer(), source));
        }

        [Fact]
        [DisplayName("POLHEM3003 ignores an interface that extends IExecFuncHandler")]
        public void ExecFunc_InterfaceExtendingHandler_ReportsNothing()
        {
            const string source = """
                using Polhem.Business;

                public interface IMaintenanceHandler : IExecFuncHandler
                {
                    void RebuildIndexes(ExecFuncArgs args, ExecFuncResult result);
                }
                """;

            Assert.Empty(AnalyzerRunner.RunOnSource(new ExecFuncAccessControlAnalyzer(), source, s_execFuncAnchors));
        }

        [Fact]
        [DisplayName("POLHEM3003 ignores two-parameter methods whose parameter types only half match the ExecFunc shape")]
        public void ExecFunc_HalfMatchingSignatures_ReportNothing()
        {
            const string source = """
                using Polhem.Business;

                public class MaintenanceExecFuncHandler : IExecFuncHandler
                {
                    public void FirstMatches(ExecFuncArgs args, string result) { }
                    public void SecondMatches(string args, ExecFuncResult result) { }
                }
                """;

            Assert.Empty(AnalyzerRunner.RunOnSource(new ExecFuncAccessControlAnalyzer(), source, s_execFuncAnchors));
        }

        [Fact]
        [DisplayName("POLHEM3001 reports an override when no method up the override chain declares access control")]
        public void BusinessObject_UnmarkedOverrideChain_ReportsEachLevel()
        {
            const string source = """
                using System;
                using Polhem.Business;
                using Polhem.Definition;

                public class BaseOrderBusinessObject : BusinessObject
                {
                    public BaseOrderBusinessObject(IBusinessObjectContext ctx, Guid accessToken)
                        : base(ctx, accessToken) { }

                    public virtual string Approve(string id) => id;
                }

                public class DerivedOrderBusinessObject : BaseOrderBusinessObject
                {
                    public DerivedOrderBusinessObject(IBusinessObjectContext ctx, Guid accessToken)
                        : base(ctx, accessToken) { }

                    public override string Approve(string id) => id;
                }
                """;

            var diagnostics = AnalyzerRunner.RunOnSource(new BusinessObjectAccessControlAnalyzer(), source, s_businessObjectAnchors);

            Assert.Equal(2, diagnostics.Length);
            Assert.All(diagnostics, d => Assert.Equal("POLHEM3001", d.Id));
        }

        [Fact]
        [DisplayName("POLHEM3001 ignores an interface and a struct even when they declare one-parameter methods")]
        public void BusinessObject_InterfaceAndStruct_ReportNothing()
        {
            const string source = """
                using Polhem.Business;

                public interface IOrderActions
                {
                    string Approve(string id);
                }

                public struct OrderValue
                {
                    public string Approve(string id) => id;
                }
                """;

            Assert.Empty(AnalyzerRunner.RunOnSource(new BusinessObjectAccessControlAnalyzer(), source, s_businessObjectAnchors));
        }
    }
}
