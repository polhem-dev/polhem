using System.Reflection;

namespace Polhem.UI.Core
{
    /// <summary>
    /// Provides version and product metadata of the currently running application or host assembly.
    /// </summary>
    public static class VersionInfo
    {
        private const string Unknown = "Unknown";

        private static Assembly EntryAssembly => Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        /// <summary>
        /// Product name, mapped to the <c>&lt;Product&gt;</c> property in the .csproj.
        /// </summary>
        public static string Product => GetAttribute<AssemblyProductAttribute>()?.Product ?? Unknown;

        /// <summary>
        /// Company name, mapped to the <c>&lt;Company&gt;</c> property in the .csproj.
        /// </summary>
        public static string Company => GetAttribute<AssemblyCompanyAttribute>()?.Company ?? Unknown;

        /// <summary>
        /// Application description, mapped to the <c>&lt;Description&gt;</c> property in the .csproj.
        /// </summary>
        public static string Description => GetAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";

        /// <summary>
        /// Clean version number (Git hash stripped), mapped to the <c>&lt;Version&gt;</c> property in the .csproj.
        /// </summary>
        public static string Version => InformationalVersion?.Split('+')[0] ?? Unknown;

        /// <summary>
        /// File version, mapped to the <c>&lt;FileVersion&gt;</c> property in the .csproj.
        /// </summary>
        /// <remarks>
        /// Read from <see cref="AssemblyFileVersionAttribute"/>, not from the file on disk, so it also works
        /// where the assembly has no file path: Android assembly stores, browser WebAssembly and
        /// single-file publishes.
        /// </remarks>
        public static string FileVersion => GetFileVersion(EntryAssembly);

        /// <summary>
        /// Assembly version, mapped to the <c>&lt;AssemblyVersion&gt;</c> property; defaults to <see cref="Version"/> + ".0" when absent.
        /// </summary>
        public static string AssemblyVersion => EntryAssembly.GetName().Version?.ToString() ?? Unknown;

        /// <summary>
        /// Full informational version (may include Git hash), mapped to <see cref="AssemblyInformationalVersionAttribute"/>.
        /// </summary>
        public static string FullInformationalVersion => InformationalVersion ?? Unknown;

        private static string? InformationalVersion =>
            GetAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        private static string GetFileVersion(Assembly assembly) =>
            assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? Unknown;

        private static T? GetAttribute<T>() where T : Attribute =>
            EntryAssembly.GetCustomAttribute<T>();
    }
}
