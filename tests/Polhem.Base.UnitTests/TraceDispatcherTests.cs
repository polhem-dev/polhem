using System.ComponentModel;
using Polhem.Base.Tracing;

namespace Polhem.Base.UnitTests
{
    public class TraceDispatcherTests
    {
        private sealed class CapturingWriter : ITraceWriter
        {
            public List<TraceEvent> Events { get; } = [];
            public void Write(TraceEvent evt) => Events.Add(evt);
        }

        [Fact]
        [DisplayName("TraceStart creates a TraceContext with the correct properties")]
        public void TraceStart_WithFullParameters_ReturnsContextWithCorrectProperties()
        {
            var listener = new TraceDispatcher(new CapturingWriter());

            var ctx = listener.TraceStart(TraceLayers.Business, "detail", name: "TestOp");

            Assert.Equal(TraceLayers.Business, ctx.Layer);
            Assert.Equal("TestOp", ctx.Name);
            Assert.Equal("detail", ctx.Detail);
            Assert.True(ctx.Stopwatch.IsRunning);
        }

        [Fact]
        [DisplayName("TraceStart without optional parameters creates a context with default values")]
        public void TraceStart_WithOnlyRequiredLayer_CreatesContextWithDefaults()
        {
            var listener = new TraceDispatcher(new CapturingWriter());

            var ctx = listener.TraceStart(TraceLayers.Data, name: "TestMethod");

            Assert.Equal(TraceLayers.Data, ctx.Layer);
            Assert.Equal(string.Empty, ctx.Category);
            Assert.Null(ctx.Tag);
            Assert.Equal(string.Empty, ctx.Detail);
        }

        [Fact]
        [DisplayName("TraceEnd stops the stopwatch and emits an End event")]
        public void TraceEnd_AfterStart_StopsStopwatchAndEmitsEndEvent()
        {
            var writer = new CapturingWriter();
            var listener = new TraceDispatcher(writer);

            var ctx = listener.TraceStart(TraceLayers.UI, name: "Op");
            listener.TraceEnd(ctx, TraceStatus.Ok);

            Assert.False(ctx.Stopwatch.IsRunning);
            Assert.Equal(2, writer.Events.Count);
            Assert.Equal(TraceEventKind.End, writer.Events[1].Kind);
        }

        [Fact]
        [DisplayName("TraceEnd with a detail overrides the Detail of the context")]
        public void TraceEnd_WithExplicitDetail_OverridesContextDetail()
        {
            var writer = new CapturingWriter();
            var listener = new TraceDispatcher(writer);

            var ctx = listener.TraceStart(TraceLayers.Data, "original-detail", name: "Op");
            listener.TraceEnd(ctx, TraceStatus.Error, "override-detail");

            Assert.Equal("override-detail", writer.Events[1].Detail);
            Assert.Equal(TraceStatus.Error, writer.Events[1].Status);
        }

        [Fact]
        [DisplayName("TraceWrite emits a Point event")]
        public void TraceWrite_WithDetailAndStatus_EmitsPointEvent()
        {
            var writer = new CapturingWriter();
            var listener = new TraceDispatcher(writer);

            listener.TraceWrite(TraceLayers.ApiServer, "write-detail", TraceStatus.Cancelled, name: "WriteOp");

            var evt = Assert.Single(writer.Events);
            Assert.Equal(TraceEventKind.Point, evt.Kind);
            Assert.Equal(TraceLayers.ApiServer, evt.Layer);
            Assert.Equal("write-detail", evt.Detail);
            Assert.Equal(TraceStatus.Cancelled, evt.Status);
            Assert.Equal(0, evt.DurationMs);
        }

        [Fact]
        [DisplayName("TraceWrite without optional parameters emits a Point event with the default status")]
        public void TraceWrite_WithOnlyRequiredLayer_EmitsDefaultStatusEvent()
        {
            var writer = new CapturingWriter();
            var listener = new TraceDispatcher(writer);

            listener.TraceWrite(TraceLayers.None, name: "TestMethod");

            var evt = Assert.Single(writer.Events);
            Assert.Equal(TraceEventKind.Point, evt.Kind);
            Assert.Equal(TraceStatus.Ok, evt.Status);
            Assert.Equal(string.Empty, evt.Detail);
            Assert.Equal(string.Empty, evt.Category);
            Assert.Null(evt.Tag);
        }
    }
}
