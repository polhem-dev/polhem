using System.ComponentModel;
using Polhem.Core.Collections;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Collections
{
    /// <summary>
    /// Tests for the parameterless constructors of the definition collections, which the deserializers use and which
    /// create a collection that belongs to no owner yet.
    /// </summary>
    public class UnownedDefinitionCollectionTests
    {
        public static TheoryData<string> CollectionNames =>
        [
            nameof(FormTableCollection),
            nameof(FormFieldCollection),
            nameof(FormRuleCollection),
            nameof(DbFieldCollection),
            nameof(DbTableIndexCollection),
            nameof(DbCategoryCollection),
            nameof(TableItemCollection),
            nameof(ProgramItemCollection),
            nameof(ProgramPluginItemCollection),
            nameof(PluginItemCollection),
            nameof(PermissionRuleCollection),
            nameof(PermissionModelCollection),
            nameof(MenuNodeCollection),
        ];

        [Theory]
        [MemberData(nameof(CollectionNames))]
        [DisplayName("A definition collection created without an owner starts empty and has no owner")]
        public void ParameterlessConstructor_CreatesEmptyUnownedCollection(string name)
        {
            var collection = Create(name);

            Assert.Empty((System.Collections.ICollection)collection);
            Assert.Null(OwnerOf(collection));
        }

        private static object? OwnerOf(object collection) => collection switch
        {
            IKeyCollectionBase keyed => keyed.Owner,
            ICollectionBase plain => plain.Owner,
            _ => throw new InvalidOperationException($"{collection.GetType().Name} is not a framework collection."),
        };

        private static object Create(string name) => name switch
        {
            nameof(FormTableCollection) => new FormTableCollection(),
            nameof(FormFieldCollection) => new FormFieldCollection(),
            nameof(FormRuleCollection) => new FormRuleCollection(),
            nameof(DbFieldCollection) => new DbFieldCollection(),
            nameof(DbTableIndexCollection) => new DbTableIndexCollection(),
            nameof(DbCategoryCollection) => new DbCategoryCollection(),
            nameof(TableItemCollection) => new TableItemCollection(),
            nameof(ProgramItemCollection) => new ProgramItemCollection(),
            nameof(ProgramPluginItemCollection) => new ProgramPluginItemCollection(),
            nameof(PluginItemCollection) => new PluginItemCollection(),
            nameof(PermissionRuleCollection) => new PermissionRuleCollection(),
            nameof(PermissionModelCollection) => new PermissionModelCollection(),
            _ => new MenuNodeCollection(),
        };
    }
}
