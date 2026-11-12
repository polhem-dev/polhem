using System.ComponentModel;
using Polhem.Definition.Logging;
using Polhem.Repository.AuditLog;

namespace Polhem.Repository.UnitTests.AuditLog
{
    /// <summary>
    /// <see cref="AuditLogWriteRepository.BuildInsert(AuditEntry, Polhem.Definition.Database.TableSchema?)"/> turns characters some databases refuse into U+FFFD, so one
    /// value from an unauthenticated caller cannot make the database refuse the batch it belongs to.
    /// </summary>
    public class AuditLogWriteRepositoryValueTests
    {
        // Built in code and kept out of discovery: a lone surrogate is replaced both when the compiler writes an attribute
        // argument to metadata and when xUnit serializes discovered theory data.
        public static TheoryData<string, string> LoneSurrogates => new()
        {
            { "ab\uD83D", "ab\uFFFD" },
            { "\uDE00ab", "\uFFFDab" },
            { "a\uD83Db\uDE00c", "a\uFFFDb\uFFFDc" },
            { "\uDE00\uD83D", "\uFFFD\uFFFD" },
            { "a\0\uD83D", "a\uFFFD\uFFFD" },
        };

        [Theory]
        [MemberData(nameof(LoneSurrogates), DisableDiscoveryEnumeration = true)]
        [DisplayName("BuildInsert replaces a lone surrogate with U+FFFD")]
        public void BuildInsert_LoneSurrogate_ReplacedWithReplacementCharacter(string userId, string expected)
        {
            Assert.Equal(expected, UserIdValue(userId));
        }

        [Theory]
        [InlineData("ab")]
        [InlineData("讚😀")]
        [InlineData("😀😀")]
        [DisplayName("BuildInsert keeps a value without a lone surrogate unchanged, surrogate pairs included")]
        public void BuildInsert_WellFormedValue_Unchanged(string userId)
        {
            Assert.Equal(userId, UserIdValue(userId));
        }

        private static string UserIdValue(string userId)
        {
            var entry = new LoginAuditEntry { SysRowId = Guid.NewGuid(), UserId = userId, Event = LoginEvent.LoginFailed };
            var spec = AuditLogWriteRepository.BuildInsert(entry, null);
            var index = entry.GetColumns().ToList().FindIndex(c => c.Name == "user_id");
            Assert.True(index >= 0, "The login entry has no user_id column.");
            return Assert.IsType<string>(spec.Parameters.ElementAt(index).Value);
        }
    }
}
