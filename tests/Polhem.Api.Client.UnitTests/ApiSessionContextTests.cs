using System.ComponentModel;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Verifies that the signed-in state is held per session and replaced as one unit.
    /// </summary>
    /// <remarks>
    /// The defects this type exists to prevent were "two sessions share one transport key" and "a token from one
    /// sign-in travels with the key of another", so the tests check isolation and whole replacement rather than
    /// that a value can be stored.
    /// </remarks>
    public class ApiSessionContextTests
    {
        private static ApiSessionCredentials Credentials(byte key, string zone = "")
            => new(Guid.NewGuid(), [key, key, key], zone);

        [Fact]
        [DisplayName("A new session is anonymous")]
        public void Credentials_NewSession_IsAnonymous()
        {
            var session = new ApiSessionContext();

            Assert.Same(ApiSessionCredentials.Anonymous, session.Credentials);
            Assert.Equal(Guid.Empty, session.Credentials.AccessToken);
            Assert.Empty(session.Credentials.ApiEncryptionKey);
            Assert.Equal(string.Empty, session.Credentials.UserTimeZoneId);
        }

        [Fact]
        [DisplayName("Two sessions do not see each other's credentials")]
        public void SignIn_TwoSessions_DoNotOverwriteEachOther()
        {
            var a = new ApiSessionContext();
            var b = new ApiSessionContext();
            var first = Credentials(1, "Asia/Taipei");
            var second = Credentials(9, "Asia/Tokyo");

            a.SignIn(first);
            b.SignIn(second);

            Assert.Same(first, a.Credentials);
            Assert.Same(second, b.Credentials);
        }

        [Fact]
        [DisplayName("SignIn replaces the token, the key and the zone together")]
        public void SignIn_ReplacesWholeCredentials()
        {
            var session = new ApiSessionContext();
            var first = Credentials(1, "Asia/Taipei");
            session.SignIn(first);
            var held = session.Credentials;

            var second = Credentials(2, "Asia/Tokyo");
            session.SignIn(second);

            // A reader that took the first instance keeps a consistent token, key and zone.
            Assert.Same(first, held);
            Assert.Same(second, session.Credentials);
        }

        [Fact]
        [DisplayName("SignOut returns the session to anonymous")]
        public void SignOut_AfterSignIn_IsAnonymous()
        {
            var session = new ApiSessionContext();
            session.SignIn(Credentials(1, "Asia/Taipei"));

            session.SignOut();

            Assert.Same(ApiSessionCredentials.Anonymous, session.Credentials);
        }

        [Fact]
        [DisplayName("SignIn with null credentials throws ArgumentNullException")]
        public void SignIn_Null_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ApiSessionContext().SignIn(null!));
        }

        [Fact]
        [DisplayName("NextSequence keeps counting across a new sign-in")]
        public void NextSequence_AcrossSignIn_KeepsIncreasing()
        {
            var session = new ApiSessionContext();
            var before = session.NextSequence();

            session.SignIn(Credentials(1));

            Assert.True(session.NextSequence() > before);
        }

        [Fact]
        [DisplayName("ApiSessionCredentials rejects a null key or zone")]
        public void Credentials_NullParts_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ApiSessionCredentials(Guid.NewGuid(), null!, string.Empty));
            Assert.Throws<ArgumentNullException>(() => new ApiSessionCredentials(Guid.NewGuid(), [], null!));
        }
    }
}
