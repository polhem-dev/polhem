namespace Blazor.Mud.DemoCenter.Modules;

/// <summary>
/// One demo case: where it sits in the navigation tree (<paramref name="Category"/> → <paramref name="Title"/>),
/// a one-line description, and the component that renders it.
/// </summary>
/// <param name="Category">The topic.</param>
/// <param name="Title">The case title.</param>
/// <param name="Description">The description shown above the case.</param>
/// <param name="Component">The Razor component of the case.</param>
public sealed record DemoModule(string Category, string Title, string Description, Type Component)
{
    /// <summary>Gets the route of the case.</summary>
    public string Href => "case/" + Component.Name;

    /// <summary>Returns the case's own <c>.razor</c> file, embedded by the project file.</summary>
    /// <returns>The source text.</returns>
    public string GetSourceText()
    {
        var resourceName = Component.FullName + ".razor";
        using var stream = Component.Assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return "Source resource not found: " + resourceName;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
