using System.ComponentModel;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Polhem.Web.Blazor.Server.UnitTests
{
    /// <summary>
    /// The Blazor head shares <c>Polhem.UI.Core</c> with the native heads but never its process-wide service entry
    /// point or the single-user bridge (ADR-054).
    /// </summary>
    /// <remarks>
    /// One server process serves many circuits. <c>Polhem.UI.Core.UIServices</c> holds one provider for the whole
    /// process, and <c>Polhem.UI.ApiBridge</c> reads one signed-in user from process-wide statics, so either would
    /// hand one user's connection and identity to every circuit. The scan reads the compiled assembly's type and
    /// assembly references, so a mention in a comment or a cref does not count and a use in code always does.
    /// </remarks>
    public class UIServicesUsageGateTests
    {
        // The shared layer, the host and the UI package, which is everything a Blazor application runs.
        // `FormDataObject` lives in `Polhem.UI.Core`, which this scan cannot see into; UICoreServiceLocatorGateTests
        // in Polhem.UI.Core.UnitTests covers it.
        private static readonly string[] s_assemblyPaths =
        [
            typeof(Polhem.Web.Blazor.Components.FormPageBase).Assembly.Location,
            typeof(Polhem.Web.Blazor.Server.Components.PolhemAccessTokenProvider).Assembly.Location,
            typeof(Polhem.Web.Blazor.Mud.Controls.GridControl).Assembly.Location,
            typeof(Polhem.Web.Blazor.Fluent.Controls.GridControl).Assembly.Location,
        ];

        private static string[] TypeReferences(MetadataReader reader)
            => reader.TypeReferences
                .Select(handle => reader.GetTypeReference(handle))
                .Select(type => $"{reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}")
                .ToArray();

        [Fact]
        [DisplayName("The Blazor head does not reference the process-wide UIServices entry point")]
        public void BlazorHead_DoesNotReferenceUIServices()
        {
            foreach (var path in s_assemblyPaths)
            {
                using var peReader = new PEReader(File.OpenRead(path));
                Assert.DoesNotContain("Polhem.UI.Core.UIServices", TypeReferences(peReader.GetMetadataReader()));
            }
        }

        [Fact]
        [DisplayName("The Blazor head does not reference the single-user Polhem.UI.ApiBridge assembly")]
        public void BlazorHead_DoesNotReferenceApiBridge()
        {
            foreach (var path in s_assemblyPaths)
            {
                using var peReader = new PEReader(File.OpenRead(path));
                var reader = peReader.GetMetadataReader();
                var assemblies = reader.AssemblyReferences
                    .Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name))
                    .ToArray();
                Assert.DoesNotContain("Polhem.UI.ApiBridge", assemblies);
            }
        }

        [Fact]
        [DisplayName("The scan sees the Polhem.UI.Core types the Blazor head does use, so it cannot pass vacuously")]
        public void Scan_SeesUICoreTypesInUse()
        {
            using var peReader = new PEReader(File.OpenRead(s_assemblyPaths[0]));
            Assert.Contains("Polhem.UI.Core.DataObjects.FormDataObject", TypeReferences(peReader.GetMetadataReader()));
        }
    }
}
