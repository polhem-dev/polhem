using System.ComponentModel;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The recent-solutions bookkeeping of <see cref="UserSettings"/>, without touching the settings file.
    /// </summary>
    public class UserSettingsTests
    {
        [Fact]
        [DisplayName("RemoveRecentSolution drops the entry whatever its casing and keeps the others in order")]
        public void RemoveRecentSolution_DifferentCasing_RemovesEntry()
        {
            var settings = new UserSettings { RecentSolutions = ["/a/Define", "/b/Define", "/c/Define"] };

            settings.RemoveRecentSolution("/B/define");

            Assert.Equal(["/a/Define", "/c/Define"], settings.RecentSolutions);
        }
    }
}
