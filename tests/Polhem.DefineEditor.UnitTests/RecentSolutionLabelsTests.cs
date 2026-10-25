using System.ComponentModel;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The names and locations the Welcome page and File → Open Recent show for the recorded DefinePath folders.
    /// </summary>
    public class RecentSolutionLabelsTests
    {
        private static readonly string s_root = Path.Combine(Path.GetTempPath(), "recent-labels");
        private static readonly string s_home = Path.Combine(s_root, "home");

        private static string Home(params string[] parts) => Path.Combine([s_home, .. parts]);

        private static string Tilde(params string[] parts) => string.Join(Path.DirectorySeparatorChar, ["~", .. parts]);

        [Fact]
        [DisplayName("A Define folder is named after the folder that contains it")]
        public void Build_DefineFolder_UsesParentName()
        {
            var recent = Assert.Single(RecentSolutionLabels.Build([Home("repos", "Northwind", "Define")], s_home, _ => true));

            Assert.Equal("Northwind", recent.Name);
            Assert.Equal(Tilde("repos"), recent.Location);
        }

        [Fact]
        [DisplayName("A Define folder is recognized whatever its casing")]
        public void Build_DefineFolderOtherCasing_UsesParentName()
        {
            var recent = Assert.Single(RecentSolutionLabels.Build([Home("repos", "Northwind", "define")], s_home, _ => true));

            Assert.Equal("Northwind", recent.Name);
        }

        [Fact]
        [DisplayName("A folder with another name keeps its own name")]
        public void Build_OtherFolder_UsesOwnName()
        {
            var recent = Assert.Single(RecentSolutionLabels.Build([Home("repos", "Settings")], s_home, _ => true));

            Assert.Equal("Settings", recent.Name);
            Assert.Equal(Tilde("repos"), recent.Location);
        }

        [Fact]
        [DisplayName("Entries with the same name grow one parent folder at a time until they differ")]
        public void Build_SameName_AddsParentsUntilUnique()
        {
            var recents = RecentSolutionLabels.Build(
                [
                    Home("a", "en", "Northwind", "Define"),
                    Home("b", "en", "Northwind", "Define"),
                    Home("repos", "Shop", "Define"),
                ],
                s_home,
                _ => true);

            var sep = Path.DirectorySeparatorChar;
            Assert.Equal($"a{sep}en{sep}Northwind", recents[0].Name);
            Assert.Equal("~", recents[0].Location);
            Assert.Equal($"b{sep}en{sep}Northwind", recents[1].Name);
            Assert.Equal("Shop", recents[2].Name);
        }

        [Fact]
        [DisplayName("Only the entries that collide grow; the others keep the short name")]
        public void Build_PartialCollision_LeavesUniqueNamesShort()
        {
            var recents = RecentSolutionLabels.Build(
                [Home("x", "Northwind", "Define"), Home("y", "Northwind", "Define"), Home("Shop", "Define")],
                s_home,
                _ => true);

            Assert.Equal("Shop", recents[2].Name);
            Assert.Equal("~", recents[2].Location);
        }

        [Fact]
        [DisplayName("A location outside the home directory is shown in full")]
        public void Build_OutsideHome_KeepsFullLocation()
        {
            var other = Path.Combine(s_root, "srv", "Northwind", "Define");

            var recent = Assert.Single(RecentSolutionLabels.Build([other], s_home, _ => true));

            Assert.Equal(Path.Combine(s_root, "srv"), recent.Location);
        }

        [Fact]
        [DisplayName("A folder that only shares the home directory's prefix is not shortened")]
        public void Build_HomePrefixSibling_IsNotShortened()
        {
            var sibling = Path.Combine(s_root, "home2", "Northwind", "Define");

            var recent = Assert.Single(RecentSolutionLabels.Build([sibling], s_home, _ => true));

            Assert.Equal(Path.Combine(s_root, "home2"), recent.Location);
        }

        [Fact]
        [DisplayName("A trailing separator does not change the name")]
        public void Build_TrailingSeparator_IsIgnored()
        {
            var path = Home("repos", "Northwind", "Define") + Path.DirectorySeparatorChar;

            var recent = Assert.Single(RecentSolutionLabels.Build([path], s_home, _ => true));

            Assert.Equal("Northwind", recent.Name);
            Assert.Equal(path, recent.Path);
        }

        [Fact]
        [DisplayName("An entry records whether its folder still exists")]
        public void Build_MissingFolder_IsMarked()
        {
            var kept = Home("repos", "Northwind", "Define");
            var gone = Home("repos", "Shop", "Define");

            var recents = RecentSolutionLabels.Build([kept, gone], s_home, path => path == kept);

            Assert.True(recents[0].Exists);
            Assert.False(recents[1].Exists);
        }

        [Fact]
        [DisplayName("The menu label joins the name and the location")]
        public void MenuLabel_JoinsNameAndLocation()
        {
            var recent = Assert.Single(RecentSolutionLabels.Build([Home("repos", "Northwind", "Define")], s_home, _ => true));

            Assert.Equal($"Northwind — {Tilde("repos")}", recent.MenuLabel);
        }
    }
}
