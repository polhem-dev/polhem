namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Locates the root of the repository working tree the tests were built from.
    /// </summary>
    /// <remarks>
    /// The root is the nearest ancestor of the test output directory that contains <c>.git</c>, whether that is a
    /// directory (a normal clone) or a file (a git worktree, whose <c>.git</c> file points at the main repository).
    /// Accepting only a directory would let a worktree walk past its own root into the main checkout, so gates and
    /// fixtures would read the main checkout's files instead of the branch under test.
    /// <para>
    /// Projects that do not reference <c>Polhem.Tests.Shared</c> compile this file in as a linked source file.
    /// </para>
    /// </remarks>
    public static class RepoRoot
    {
        /// <summary>
        /// Finds the repository root above <see cref="AppContext.BaseDirectory"/>.
        /// </summary>
        /// <returns>The absolute path of the working tree root.</returns>
        /// <exception cref="InvalidOperationException">No ancestor contains <c>.git</c>.</exception>
        public static string Find() => Find(AppContext.BaseDirectory);

        /// <summary>
        /// Finds the repository root at or above <paramref name="startDirectory"/>.
        /// </summary>
        /// <param name="startDirectory">The directory to start the walk from.</param>
        /// <returns>The absolute path of the working tree root.</returns>
        /// <exception cref="InvalidOperationException">No ancestor contains <c>.git</c>.</exception>
        public static string Find(string startDirectory)
        {
            ArgumentNullException.ThrowIfNull(startDirectory);
            var dir = new DirectoryInfo(startDirectory);
            while (dir != null)
            {
                string gitPath = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(gitPath) || File.Exists(gitPath))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new InvalidOperationException($"Cannot find the repository root (.git) above '{startDirectory}'.");
        }
    }
}
