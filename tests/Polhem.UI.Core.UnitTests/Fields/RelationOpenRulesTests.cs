using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.UnitTests.Fields
{
    /// <summary>
    /// Tests <see cref="RelationOpenRules"/>: when a relation field opens its related record, which record, and
    /// whether the user may see it.
    /// </summary>
    public class RelationOpenRulesTests
    {
        private static readonly Guid s_rowId = new("bbbbbbbb-0000-0000-0000-000000000001");

        private static FormField RelationField() => new("leave_type_rowid", "Leave type", FieldDbType.Guid) { RelationProgId = "LeaveType" };

        private static FormField PlainField() => new("remark", "Remark", FieldDbType.String);

        private static FormSchema RelationSchema(string permissionModelId = "")
            => new("LeaveType", "Leave type") { PermissionModelId = permissionModelId };

        [Theory]
        [InlineData(SingleFormMode.View, true)]
        [InlineData(SingleFormMode.Edit, false)]
        [InlineData(SingleFormMode.Add, false)]
        [DisplayName("A relation field offers to open its record only in View mode")]
        public void IsAvailable_RelationField_OnlyInViewMode(SingleFormMode formMode, bool expected)
        {
            Assert.Equal(expected, RelationOpenRules.IsAvailable(formMode, RelationField()));
        }

        [Fact]
        [DisplayName("A field without a RelationProgId never offers to open a record")]
        public void IsAvailable_PlainField_ReturnsFalse()
        {
            Assert.False(RelationOpenRules.IsAvailable(SingleFormMode.View, PlainField()));
            Assert.False(RelationOpenRules.IsAvailable(SingleFormMode.View, null));
        }

        [Fact]
        [DisplayName("A Guid value names the related form and the row")]
        public void TryGetTarget_GuidValue_ReturnsTarget()
        {
            Assert.True(RelationOpenRules.TryGetTarget(RelationField(), s_rowId, out var target));
            Assert.Equal(new RelationTarget("LeaveType", s_rowId), target);
        }

        [Fact]
        [DisplayName("The string form of a Guid, as a data object returns it, names the row")]
        public void TryGetTarget_StringValue_ReturnsTarget()
        {
            Assert.True(RelationOpenRules.TryGetTarget(RelationField(), s_rowId.ToString(), out var target));
            Assert.Equal(s_rowId, target.RowId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not a guid")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        [DisplayName("An empty value, an empty Guid or a value that is not a row id names no record")]
        public void TryGetTarget_EmptyOrInvalidValue_ReturnsFalse(string? value)
        {
            Assert.False(RelationOpenRules.TryGetTarget(RelationField(), value, out _));
        }

        [Fact]
        [DisplayName("A DBNull value names no record")]
        public void TryGetTarget_DBNull_ReturnsFalse()
        {
            Assert.False(RelationOpenRules.TryGetTarget(RelationField(), DBNull.Value, out _));
        }

        [Fact]
        [DisplayName("A field without a RelationProgId names no record even when its value is a Guid")]
        public void TryGetTarget_PlainField_ReturnsFalse()
        {
            Assert.False(RelationOpenRules.TryGetTarget(PlainField(), s_rowId, out _));
            Assert.False(RelationOpenRules.TryGetTarget(null, s_rowId, out _));
        }

        [Fact]
        [DisplayName("Without a capability snapshot the user may open the related record")]
        public void CanOpen_NoSnapshot_ReturnsTrue()
        {
            Assert.True(RelationOpenRules.CanOpen(RelationSchema("LeaveType"), null));
        }

        [Fact]
        [DisplayName("A related form without a permission model may be opened by anyone")]
        public void CanOpen_NoPermissionModel_ReturnsTrue()
        {
            var capabilities = new Dictionary<string, PermissionActions>(StringComparer.Ordinal);
            Assert.True(RelationOpenRules.CanOpen(RelationSchema(), capabilities));
        }

        [Fact]
        [DisplayName("A user with Read on the related form's model may open its record")]
        public void CanOpen_ReadGranted_ReturnsTrue()
        {
            var capabilities = new Dictionary<string, PermissionActions>(StringComparer.Ordinal)
            {
                ["LeaveType"] = PermissionActions.Read,
            };
            Assert.True(RelationOpenRules.CanOpen(RelationSchema("LeaveType"), capabilities));
        }

        [Fact]
        [DisplayName("A user without Read on the related form's model may not open its record, whatever else they hold")]
        public void CanOpen_ReadMissing_ReturnsFalse()
        {
            var capabilities = new Dictionary<string, PermissionActions>(StringComparer.Ordinal)
            {
                ["LeaveType"] = PermissionActions.Create | PermissionActions.Update,
                ["Other"] = PermissionActions.Read,
            };
            Assert.False(RelationOpenRules.CanOpen(RelationSchema("LeaveType"), capabilities));
        }
    }
}
