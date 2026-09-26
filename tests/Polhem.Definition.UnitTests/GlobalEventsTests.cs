using System.ComponentModel;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Tests for GlobalEvents.
    /// </summary>
    [Collection(ProcessWideStateCollection.Name)]
    public class GlobalEventsTests
    {
        [Fact]
        [DisplayName("RaiseDatabaseSettingsChanged invokes a subscribed handler")]
        public void RaiseDatabaseSettingsChanged_InvokesSubscribedHandler()
        {
            var invoked = 0;
            EventHandler handler = (sender, e) => invoked++;
            GlobalEvents.DatabaseSettingsChanged += handler;
            try
            {
                GlobalEvents.RaiseDatabaseSettingsChanged();

                Assert.Equal(1, invoked);
            }
            finally
            {
                GlobalEvents.DatabaseSettingsChanged -= handler;
            }
        }

        [Fact]
        [DisplayName("RaiseDatabaseSettingsChanged invokes the handler with sender null and EventArgs.Empty")]
        public void RaiseDatabaseSettingsChanged_PassesNullSenderAndEmptyArgs()
        {
            object? capturedSender = new object();
            EventArgs? capturedArgs = null;
            EventHandler handler = (sender, e) =>
            {
                capturedSender = sender;
                capturedArgs = e;
            };
            GlobalEvents.DatabaseSettingsChanged += handler;
            try
            {
                GlobalEvents.RaiseDatabaseSettingsChanged();

                Assert.Null(capturedSender);
                Assert.Same(EventArgs.Empty, capturedArgs);
            }
            finally
            {
                GlobalEvents.DatabaseSettingsChanged -= handler;
            }
        }

        [Fact]
        [DisplayName("An unsubscribed handler is not invoked")]
        public void Unsubscribe_HandlerNotInvoked()
        {
            var invoked = 0;
            EventHandler handler = (sender, e) => invoked++;

            GlobalEvents.DatabaseSettingsChanged += handler;
            GlobalEvents.DatabaseSettingsChanged -= handler;

            GlobalEvents.RaiseDatabaseSettingsChanged();

            Assert.Equal(0, invoked);
        }
    }
}
