using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers <see cref="ClientInfo.ResetDefineCache"/>.
    /// Verifies that it does not throw when _defineAccess is null (a no-op),
    /// and that calling ClearCache does not throw when _defineAccess is a ClientDefineAccess.
    /// It mutates static state, so it runs serially in the ClientInfoState collection.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoResetDefineCacheTests
    {
        private static readonly FieldInfo s_defineAccessField =
            typeof(ClientInfo).GetField("s_defineAccess", BindingFlags.NonPublic | BindingFlags.Static)!;

        [Fact]
        [DisplayName("ResetDefineCache returns without throwing when _defineAccess is null")]
        public void ResetDefineCache_WhenDefineAccessIsNull_DoesNotThrow()
        {
            var original = s_defineAccessField.GetValue(null);
            try
            {
                s_defineAccessField.SetValue(null, null);
                var exception = Record.Exception(() => ClientInfo.ResetDefineCache());
                Assert.Null(exception);
            }
            finally
            {
                s_defineAccessField.SetValue(null, original);
            }
        }

        [Fact]
        [DisplayName("ResetDefineCache calls ClearCache without throwing when _defineAccess is a ClientDefineAccess")]
        public void ResetDefineCache_WhenDefineAccessIsClientDefineAccess_DoesNotThrow()
        {
            var original = s_defineAccessField.GetValue(null);
            try
            {
                var connector = PolhemApiClient.CreateLocal(EmptyServiceProvider.Instance).System;
                var remoteAccess = new ClientDefineAccess(connector);
                s_defineAccessField.SetValue(null, remoteAccess);
                var exception = Record.Exception(() => ClientInfo.ResetDefineCache());
                Assert.Null(exception);
            }
            finally
            {
                s_defineAccessField.SetValue(null, original);
            }
        }

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public static readonly EmptyServiceProvider Instance = new();

            public object? GetService(Type serviceType) => null;
        }
    }
}
