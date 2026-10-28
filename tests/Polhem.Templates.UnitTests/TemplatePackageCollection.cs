namespace Polhem.Templates.UnitTests;

/// <summary>
/// Shares one <see cref="TemplatePackageFixture"/> between the test classes of the templates, so the framework packages
/// and the template package are built and packed once per run rather than once per template.
/// </summary>
[CollectionDefinition(Name)]
public sealed class TemplatePackageCollection : ICollectionFixture<TemplatePackageFixture>
{
    public const string Name = "Template package";
}
