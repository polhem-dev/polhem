using System.ComponentModel;

namespace Polhem.Definition.Drafting.UnitTests
{
    public class DraftNamingTests
    {
        [Theory]
        [InlineData("Leave Type", "leave_type")]
        [InlineData("leaveType", "leave_type")]
        [InlineData("LeaveType", "leave_type")]
        [InlineData("HTTPServer", "http_server")]
        [InlineData("  start-date ", "start_date")]
        [InlineData("employee__id", "employee_id")]
        [InlineData("2nd approver", "f_2nd_approver")]
        [InlineData("請假類別", "")]
        [InlineData("", "")]
        [DisplayName("ToSnakeCase writes lowercase ASCII words joined by underscores")]
        public void ToSnakeCase_VariousInputs_ReturnsSnakeCase(string input, string expected)
        {
            Assert.Equal(expected, DraftNaming.ToSnakeCase(input));
        }

        [Theory]
        [InlineData("leave request", "LeaveRequest")]
        [InlineData("LeaveRequest", "LeaveRequest")]
        [InlineData("leave_request_detail", "LeaveRequestDetail")]
        [InlineData("3d print", "F3dPrint")]
        [InlineData("請假單", "")]
        [DisplayName("ToPascalCase capitalizes each ASCII word and drops the separators")]
        public void ToPascalCase_VariousInputs_ReturnsPascalCase(string input, string expected)
        {
            Assert.Equal(expected, DraftNaming.ToPascalCase(input));
        }
    }
}
