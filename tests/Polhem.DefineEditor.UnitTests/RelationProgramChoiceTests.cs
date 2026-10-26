using System.ComponentModel;
using Polhem.Definition.Settings;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The forms the relation dialog offers: built from ProgramSettings alone, and filtered by keyword.
    /// </summary>
    public sealed class RelationProgramChoiceTests
    {
        private static readonly string[] s_formSchemas = ["Customer", "CustomerGroup", "Order", "Unregistered"];

        private static ProgramSettings Settings()
        {
            var settings = new ProgramSettings();
            settings.Items!.Add(new ProgramItem { ProgId = "System", DisplayName = "System" });
            settings.Items!.Add(new ProgramItem { ProgId = "Order", DisplayName = "Orders" });
            settings.Items!.Add(new ProgramItem { ProgId = "Customer", DisplayName = "Customers" });
            settings.Items!.Add(new ProgramItem { ProgId = "CustomerGroup", DisplayName = "Customer Groups" });
            settings.Items!.Add(new ProgramItem { ProgId = "AccountCustomer", DisplayName = "Accounts" });
            return settings;
        }

        [Fact]
        [DisplayName("The choices follow ProgramSettings order and name, and a program without a FormSchema cannot be picked")]
        public void Build_FromProgramSettings()
        {
            var choices = RelationProgramChoice.Build(Settings(), s_formSchemas, currentProgId: null);

            Assert.Equal(["System", "Order", "Customer", "CustomerGroup", "AccountCustomer"], choices.Select(c => c.ProgId));
            Assert.Equal("Customers", choices[2].DisplayName);
            Assert.False(choices[0].IsSelectable);
            Assert.True(choices[2].IsSelectable);
            Assert.DoesNotContain(choices, c => c.ProgId == "Unregistered");
        }

        [Fact]
        [DisplayName("A current related form that ProgramSettings does not register is put first and marked unregistered")]
        public void Build_UnregisteredCurrent_IsFirst()
        {
            var choices = RelationProgramChoice.Build(Settings(), s_formSchemas, currentProgId: "Unregistered");

            Assert.Equal("Unregistered", choices[0].ProgId);
            Assert.False(choices[0].IsRegistered);
            Assert.True(choices[0].IsSelectable);
        }

        [Fact]
        [DisplayName("Without ProgramSettings every FormSchema is offered without a name")]
        public void Build_NoProgramSettings_FallsBackToFormSchemas()
        {
            var choices = RelationProgramChoice.Build(null, s_formSchemas, currentProgId: null);

            Assert.Equal(s_formSchemas, choices.Select(c => c.ProgId));
            Assert.All(choices, c => Assert.Equal(string.Empty, c.DisplayName));
        }

        [Fact]
        [DisplayName("The filter matches the ProgId or the name ignoring case and puts ProgIds that start with the keyword first")]
        public void Filter_MatchesIdOrName()
        {
            var choices = RelationProgramChoice.Build(Settings(), s_formSchemas, currentProgId: null);

            Assert.Equal(["Customer", "CustomerGroup", "AccountCustomer"],
                RelationProgramChoice.Filter(choices, "cust").Select(c => c.ProgId));
            Assert.Equal(["Order"], RelationProgramChoice.Filter(choices, "ORDERS").Select(c => c.ProgId));
            Assert.Equal(["AccountCustomer"], RelationProgramChoice.Filter(choices, "accounts").Select(c => c.ProgId));
            Assert.Equal(choices.Count, RelationProgramChoice.Filter(choices, " ").Count);
        }
    }
}
