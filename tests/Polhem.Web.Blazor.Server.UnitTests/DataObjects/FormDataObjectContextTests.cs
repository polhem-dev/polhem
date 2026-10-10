using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.DataObjects
{
    /// <summary>
    /// <see cref="FormDataObject"/>'s async methods must write their results on the context they were
    /// called from, which for a component is the circuit's dispatcher.
    /// </summary>
    public class FormDataObjectContextTests
    {
        /// <summary>
        /// A single-threaded context standing in for a circuit's dispatcher. It runs every posted
        /// callback on its own thread and counts them.
        /// </summary>
        private sealed class DispatcherContext : SynchronizationContext, IDisposable
        {
            private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];
            private readonly Thread _thread;
            private int _posts;

            public DispatcherContext()
            {
                _thread = new Thread(() =>
                {
                    SetSynchronizationContext(this);
                    foreach (var (callback, state) in _queue.GetConsumingEnumerable())
                        callback(state);
                }) { IsBackground = true };
                _thread.Start();
            }

            public int Posts => Volatile.Read(ref _posts);

            public override void Post(SendOrPostCallback d, object? state)
            {
                Interlocked.Increment(ref _posts);
                _queue.Add((d, state));
            }

            public Task RunAsync(Func<Task> action)
            {
                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Post(_ =>
                {
                    Task task;
                    try { task = action(); }
                    catch (InvalidOperationException ex) { done.SetException(ex); return; }
                    task.ContinueWith(t =>
                    {
                        if (t.Exception is { } ex) done.SetException(ex.InnerExceptions);
                        else done.SetResult();
                    }, TaskScheduler.Default);
                }, null);
                return done.Task;
            }

            public void Dispose()
            {
                _queue.CompleteAdding();
                _thread.Join();
                _queue.Dispose();
            }
        }

        /// <summary>
        /// Returns a pending <see cref="GetDataAsync"/> task that the test completes from a thread-pool
        /// thread, so the method under test really suspends there.
        /// </summary>
        private sealed class PendingConnector() : FormApiConnector(Polhem.Api.Client.PolhemApiClient.CreateLocal(Polhem.Tests.Shared.EmptyServiceProvider.Instance), "Employee")
        {
            public TaskCompletionSource<GetDataResponse> Pending { get; } = new();

            public override Task<GetDataResponse> GetDataAsync(Guid rowId, CancellationToken cancellationToken = default) => Pending.Task;
        }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("emp_name", "Name", FieldDbType.String);
            return schema;
        }

        [Fact]
        [DisplayName("LoadAsync resumes on the calling context before it replaces the DataSet")]
        public async Task LoadAsync_CalledOnDispatcher_ResumesOnDispatcher()
        {
            var rowId = Guid.NewGuid();
            var serverData = new DataSet("Employee");
            var table = serverData.Tables.Add("Employee");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Rows.Add(rowId);
            var connector = new PendingConnector();
            var dataObject = new FormDataObject(BuildSchema(), connector);
            using var dispatcher = new DispatcherContext();

            await dispatcher.RunAsync(() =>
            {
                var load = dataObject.LoadAsync(rowId);
                ThreadPool.QueueUserWorkItem(_ => connector.Pending.SetResult(new GetDataResponse { DataSet = serverData }));
                return load;
            });

            // One post starts the call; the second is LoadAsync's own continuation coming back to
            // the dispatcher. Without it the continuation ran on the thread pool.
            Assert.Equal(2, dispatcher.Posts);
            Assert.Same(serverData, dataObject.DataSet);
            Assert.False(dataObject.IsLoading);
        }
    }
}
