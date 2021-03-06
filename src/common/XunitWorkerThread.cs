using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xunit.Sdk
{
    class XunitWorkerThread : IDisposable
    {
        readonly ManualResetEvent finished = new ManualResetEvent(false);
        static readonly TaskFactory taskFactory = new TaskFactory();

        public XunitWorkerThread(Action threadProc)
        {
            QueueUserWorkItem(threadProc, finished);
        }

        public void Dispose()
        {
            finished.Dispose();
        }

        public void Join()
        {
            finished.WaitOne();
        }

        public static void QueueUserWorkItem(Action backgroundTask, EventWaitHandle? finished = null)
        {
            taskFactory.StartNew(_ =>
                                 {
                                     var state = (State)_!;

                                     try
                                     {
                                         state.BackgroundTask();
                                     }
                                     finally
                                     {
                                         if (state.Finished != null)
                                             state.Finished.Set();
                                     }
                                 },
                                 new State(backgroundTask, finished),
                                 CancellationToken.None,
                                 TaskCreationOptions.LongRunning,
                                 TaskScheduler.Default);
        }

        class State
        {
            public State(Action backgroundTask, EventWaitHandle? finished = null)
            {
                Guard.ArgumentNotNull(nameof(backgroundTask), backgroundTask);

                BackgroundTask = backgroundTask;
                Finished = finished;
            }

            public Action BackgroundTask { get; }

            public EventWaitHandle? Finished { get; }
        }
    }
}
