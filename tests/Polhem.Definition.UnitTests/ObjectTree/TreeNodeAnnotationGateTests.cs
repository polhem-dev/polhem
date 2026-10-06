using System.Collections;
using System.ComponentModel;
using System.Reflection;
using Polhem.Core.Collections;
using Polhem.Definition.Attributes;

namespace Polhem.Definition.UnitTests.ObjectTree
{
    /// <summary>
    /// Pins the conventions the <c>[TreeNode]</c> annotations on the definition types follow, so that a new
    /// definition type or collection cannot drift from them unnoticed.
    /// </summary>
    public class TreeNodeAnnotationGateTests
    {
        private static readonly Assembly s_definitionAssembly = typeof(TreeNodeAttribute).Assembly;

        private static TreeNodeAttribute? GetAttribute(Type type)
        {
            return TypeDescriptor.GetAttributes(type)[typeof(TreeNodeAttribute)] as TreeNodeAttribute;
        }

        private static bool IsCollection(Type type)
        {
            return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
        }

        private static bool IsDeclaredByCollectionBase(Type declaringType)
        {
            if (declaringType == typeof(CollectionItem) || declaringType == typeof(KeyCollectionItem)) { return true; }
            if (!declaringType.IsGenericType) { return false; }
            var definition = declaringType.GetGenericTypeDefinition();
            return definition == typeof(CollectionBase<>) || definition == typeof(KeyCollectionBase<>);
        }

        private static IEnumerable<Type> AnnotatedTypes()
        {
            return s_definitionAssembly.GetTypes().Where(t => t.IsClass && GetAttribute(t) != null);
        }

        /// <summary>
        /// The annotated collection properties of an annotated, non-collection type, by the same rules
        /// <see cref="Polhem.Definition.ObjectTree.ObjectTreeBuilder"/> follows.
        /// </summary>
        private static List<PropertyDescriptor> AnnotatedCollectionProperties(Type owner)
        {
            return TypeDescriptor.GetProperties(owner).Cast<PropertyDescriptor>()
                .Where(p => p.Attributes[typeof(TreeNodeIgnoreAttribute)] == null)
                .Where(p => !IsDeclaredByCollectionBase(p.ComponentType))
                .Where(p => IsCollection(p.PropertyType) && GetAttribute(p.PropertyType) != null)
                .ToList();
        }

        [Fact]
        [DisplayName("A collection is a folder exactly when its owner has more than one annotated collection property")]
        public void CollectionFolder_MatchesSiblingCollectionCount()
        {
            var violations = new List<string>();
            // Owners include unannotated types too: a document root such as `LanguageResource`, or an item of an
            // annotated collection, still gets a node and has its collection properties followed.
            foreach (var owner in s_definitionAssembly.GetTypes().Where(t => t.IsClass && !IsCollection(t)))
            {
                var properties = AnnotatedCollectionProperties(owner);
                var expected = properties.Count > 1;
                foreach (var property in properties)
                {
                    var actual = GetAttribute(property.PropertyType)!.CollectionFolder;
                    if (actual != expected)
                        violations.Add($"{owner.Name}.{property.Name} ({property.PropertyType.Name}): CollectionFolder is {actual}, expected {expected}");
                }
            }

            Assert.Empty(violations);
        }

        [Fact]
        [DisplayName("Every annotated non-collection type names the properties its label is formatted from")]
        public void TreeNode_OnObjectTypes_HasExplicitLabel()
        {
            var bare = AnnotatedTypes()
                .Where(t => !IsCollection(t))
                .Where(t => string.IsNullOrEmpty(GetAttribute(t)!.PropertyName) && string.IsNullOrEmpty(GetAttribute(t)!.DisplayFormat))
                .Select(t => t.FullName)
                .ToList();

            Assert.Empty(bare);
        }

        [Fact]
        [DisplayName("Every property a TreeNode label names exists on the annotated type")]
        public void TreeNode_PropertyNames_Exist()
        {
            var missing = new List<string>();
            foreach (var type in AnnotatedTypes())
            {
                var attribute = GetAttribute(type)!;
                if (string.IsNullOrEmpty(attribute.PropertyName)) { continue; }
                var properties = TypeDescriptor.GetProperties(type);
                foreach (var name in attribute.PropertyName.Split(',', StringSplitOptions.TrimEntries))
                {
                    if (properties[name] == null)
                        missing.Add($"{type.Name}.{name}");
                }
            }

            Assert.Empty(missing);
        }
    }
}
