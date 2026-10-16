using System.ComponentModel;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Polhem.Web.Blazor.Server.DataObjects;

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
        private static MetadataReader OpenBlazorAssembly(out PEReader peReader)
        {
            var path = typeof(FormDataObject).Assembly.Location;
            peReader = new PEReader(File.OpenRead(path));
            return peReader.GetMetadataReader();
        }

        private static string[] TypeReferences(MetadataReader reader)
            => reader.TypeReferences
                .Select(handle => reader.GetTypeReference(handle))
                .Select(type => $"{reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}")
                .ToArray();

        [Fact]
        [DisplayName("The Blazor head does not reference the process-wide UIServices entry point")]
        public void BlazorHead_DoesNotReferenceUIServices()
        {
            var reader = OpenBlazorAssembly(out var peReader);
            using (peReader)
            {
                Assert.DoesNotContain("Polhem.UI.Core.UIServices", TypeReferences(reader));
            }
        }

        [Fact]
        [DisplayName("The Blazor head does not reference the single-user Polhem.UI.ApiBridge assembly")]
        public void BlazorHead_DoesNotReferenceApiBridge()
        {
            var reader = OpenBlazorAssembly(out var peReader);
            using (peReader)
            {
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
            var reader = OpenBlazorAssembly(out var peReader);
            using (peReader)
            {
                Assert.Contains("Polhem.UI.Core.FormValueBinding", TypeReferences(reader));
            }
        }
    }
}
