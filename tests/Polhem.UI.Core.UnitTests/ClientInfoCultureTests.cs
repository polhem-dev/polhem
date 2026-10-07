using System.ComponentModel;
using System.Globalization;
using Polhem.Api.Core.Messages.System;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Tests for the user's culture on the client: <see cref="ClientInfo.ApplyLoginResult"/> takes it
    /// from the login response and makes it the process culture, and the client-wide definition
    /// loader.
    /// </summary>
    /// <remarks>
    /// Applying a culture sets the process defaults (<see cref="CultureInfo.DefaultThreadCurrentCulture"/>),
    /// so these tests share the <c>ClientInfoState</c> collection with every other class that touches
    /// <see cref="ClientInfo"/> and restore the previous values in <c>finally</c>.
    /// </remarks>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoCultureTests
    {
        [Fact]
        [DisplayName("ApplyLoginResult copies the user's culture into UserInfo and makes it the current and default culture")]
        public void ApplyLoginResult_WithCulture_AppliesUserCulture()
        {
            var saved = CultureSnapshot.Take();
            try
            {
                ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.NewGuid(), UserId = "u1", Culture = "de-DE" });

                Assert.Equal("de-DE", ClientInfo.UserInfo!.Culture);
                Assert.Equal("de-DE", CultureInfo.CurrentUICulture.Name);
                Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name);
                Assert.Equal("de-DE", CultureInfo.DefaultThreadCurrentUICulture?.Name);
                Assert.Equal("de-DE", CultureInfo.DefaultThreadCurrentCulture?.Name);
            }
            finally
            {
                ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.Empty });
                saved.Restore();
            }
        }

        [Fact]
        [DisplayName("ApplyLoginResult with no culture leaves UserInfo.Culture empty and the process culture as it was")]
        public void ApplyLoginResult_WithoutCulture_KeepsProcessCulture()
        {
            var saved = CultureSnapshot.Take();
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

                ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.NewGuid(), UserId = "u1" });

                Assert.Empty(ClientInfo.UserInfo!.Culture);
                Assert.Equal("fr-FR", CultureInfo.CurrentUICulture.Name);
            }
            finally
            {
                ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.Empty });
                saved.Restore();
            }
        }

        [Fact]
        [DisplayName("ApplyCulture refuses a name this runtime does not know and changes nothing")]
        public void ApplyCulture_UnknownName_ReturnsFalse()
        {
            var saved = CultureSnapshot.Take();
            try
            {
                Assert.False(ClientInfo.ApplyCulture("@@ not a culture"));
                Assert.False(ClientInfo.ApplyCulture(string.Empty));
                Assert.Equal(saved.UICulture, CultureInfo.CurrentUICulture);
            }
            finally
            {
                saved.Restore();
            }
        }

        [Fact]
        [DisplayName("ApplyCulture posts the culture to the caller's SynchronizationContext, which sets it on a thread that already has its own")]
        public void ApplyCulture_WithSynchronizationContext_PostsCultureToIt()
        {
            var saved = CultureSnapshot.Take();
            var savedContext = SynchronizationContext.Current;
            var context = new RecordingSynchronizationContext();
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);

                Assert.True(ClientInfo.ApplyCulture("de-DE"));

                var posted = Assert.Single(context.Posted);
                // The UI thread under Avalonia 12.1 has an explicit culture of its own, so the defaults the call
                // also sets would not reach it. Run the posted callback on a thread in the same state.
                string? observed = null;
                var thread = new Thread(() =>
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
                    posted.Callback(posted.State);
                    observed = CultureInfo.CurrentUICulture.Name + "|" + CultureInfo.CurrentCulture.Name;
                });
                thread.Start();
                thread.Join();

                Assert.Equal("de-DE|de-DE", observed);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(savedContext);
                saved.Restore();
            }
        }

        [Fact]
        [DisplayName("UseDefinitionLoader is on by default, so views localize without the host opting in")]
        public void UseDefinitionLoader_DefaultsToTrue()
        {
            // Every other test that changes the switch sits in this class and restores it, so the value
            // seen here is the initializer's.
            Assert.True(ClientInfo.UseDefinitionLoader);
        }

        [Fact]
        [DisplayName("DefinitionLoader is null until UseDefinitionLoader is on, and is rebuilt when the access token changes")]
        public void DefinitionLoader_FollowsSwitchAndToken()
        {
            bool savedSwitch = ClientInfo.UseDefinitionLoader;
            try
            {
                ClientInfo.UseDefinitionLoader = false;
                Assert.Null(ClientInfo.DefinitionLoader);

                ClientInfo.UseDefinitionLoader = true;
                var first = ClientInfo.DefinitionLoader;
                Assert.NotNull(first);
                Assert.Same(first, ClientInfo.DefinitionLoader);

                ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.NewGuid() });
                Assert.NotSame(first, ClientInfo.DefinitionLoader);
            }
            finally
            {
                ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.Empty });
                ClientInfo.UseDefinitionLoader = savedSwitch;
            }
        }

        private sealed class RecordingSynchronizationContext : SynchronizationContext
        {
            public List<(SendOrPostCallback Callback, object? State)> Posted { get; } = [];

            public override void Post(SendOrPostCallback d, object? state) => Posted.Add((d, state));
        }

        private sealed record CultureSnapshot(
            CultureInfo Culture, CultureInfo UICulture, CultureInfo? DefaultCulture, CultureInfo? DefaultUICulture)
        {
            public static CultureSnapshot Take() => new(
                CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture,
                CultureInfo.DefaultThreadCurrentCulture, CultureInfo.DefaultThreadCurrentUICulture);

            public void Restore()
            {
                CultureInfo.DefaultThreadCurrentCulture = DefaultCulture;
                CultureInfo.DefaultThreadCurrentUICulture = DefaultUICulture;
                CultureInfo.CurrentCulture = Culture;
                CultureInfo.CurrentUICulture = UICulture;
            }
        }
    }
}
