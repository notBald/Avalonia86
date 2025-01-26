using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace _86BoxManager.Tools
{
    /// <summary>
    /// Represents a task that can be executed in the background with an required UI update.
    /// 
    /// This class encapsulates a background task and an associated UI task. The background task
    /// performs work off the UI thread, while the UI task updates the UI thread with the results.
    /// 
    /// The task can be marked as canceled using the Canceled property. Once set to true, this flag
    /// cannot be reverted to false, ensuring that the task remains in a canceled state.
    /// </summary>
    public class BackgroundTask
    {
        private bool _cancel;

        /// <summary>
        /// Set this flag to true if you're not interested in this task anymore.
        /// This does not guarantee that it will be ignored, though.
        /// 
        /// This property is designed to be write-once. Once set to true, it cannot be reverted to false.
        /// This ensures that once a task is marked as canceled, it remains in that state, reflecting an irreversible state change.
        /// </summary>
        public bool Canceled 
        { 
            get => _cancel;
            set { if (value) _cancel = true; } 
        }

        /// <summary>
        /// Work to be done
        /// </summary>
        public readonly Func<object> Work;

        /// <summary>
        /// Work to be done on the UI thread
        /// </summary>
        public readonly Action<object> UIWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundTask"/> class with the specified work and UI work actions.
        /// 
        /// The <paramref name="work"/> action represents the background task to be executed off the UI thread.
        /// The <paramref name="uIWork"/> action represents the task to be executed on the UI thread, typically to update the UI with the results of the background task.
        /// 
        /// Both parameters are required and cannot be null.
        /// </summary>
        /// <param name="work">The background task to be executed.</param>
        /// <param name="uIWork">The UI task to be executed after the background task completes.</param>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="work"/> or <paramref name="uIWork"/> is null.</exception>
        public BackgroundTask(Func<object> work, Action<object> uIWork)
        {
            if (work == null || uIWork == null)
                throw new ArgumentNullException("Neither work nor uIWork can be null");
            Work = work;
            UIWork = uIWork;
        }
    }

    /// <summary>
    /// Handles background processing. The intent is to not lock up the UI while information about the
    /// virtual machine is gathered. There is also a feature to cancel the a job, as oftentimes is useful
    /// in a UI (where data that takes too long to arrive might not be needed anymore as the user have
    /// clicked on another option).
    /// </summary>
    public class BackgroundExecutor
    {
        private readonly BlockingCollection<BackgroundTask> _taskQueue = [];
        private bool _isRunning = true;
        private int _isProcessing = 0;

        public void Post(BackgroundTask task)
        {
            _taskQueue.Add(task);
            // Offload ProcessQueue to a thread pool thread to avoid blocking the UI thread.
            Task.Run(() => ProcessQueue());
        }

        private void ProcessQueue()
        {
            // Check if a thread pool thread is already processing tasks.
            // Atomically set the processing flag to true.
            // An alternative to this is "Monitor.TryEnter"
            if (Interlocked.Exchange(ref _isProcessing, 1) == 1)
                return;

            try
            {
                foreach (var task in _taskQueue.GetConsumingEnumerable())
                {
                    try 
                    {
                        if (!task.Canceled && _isRunning)
                        {
                            var res = task.Work();

                            if (!task.Canceled)
                            {
                                Dispatcher.UIThread.Post(() =>
                                {
                                    if (!task.Canceled && _isRunning)
                                        task.UIWork(res);
                                });
                            }
                        }
                    }
                    catch { /* What will happen is that the UI won't get updated. Not ideal, but I prefer this to an error message. */ Debug.Assert(false); }
                }
            }
            finally
            {
                _isProcessing = 0;

                // Re-check the queue and re-trigger ProcessQueue if there are pending tasks.
                // This because a job might have been added in between terminating the foreach
                // and setting _isProcessing = 0
                if (!_taskQueue.IsCompleted && _taskQueue.Count > 0)
                {
                    //Using await here can theoretically lead to a stack overflow.
                    _ = Task.Run(() => ProcessQueue());
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;

            //This method will cause "Post" to throw InvalidOperationException.
            _taskQueue.CompleteAdding();
        }
    }


    //public class BackgroundWorker
    //{
    //    /// <summary>
    //    /// A thread-safe collection to store tasks to be executed.
    //    /// </summary>
    //    /// <remarks>In this impl. this object will only be touched by the UI thread.</remarks>
    //    private readonly BlockingCollection<Action> _taskQueue = new BlockingCollection<Action>();

    //    /// <summary>
    //    /// A semaphore to ensure only one task is processed at a time.
    //    /// </summary>
    //    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    //    private bool _isRunning = true;

    //    public void Post(Action task)
    //    {
    //        _taskQueue.Add(task);
    //        ProcessQueue();
    //    }

    //    private async void ProcessQueue()
    //    {
    //        //Await here is not entierly what I want. My wish is that if there is no threadpool thread working on the problem, start it. If there is, just continue.
    //        //This impl. seems to me to results in a potential a lot of awaits, until the _semaphore is released, then uh.
    //        await _semaphore.WaitAsync();
    //        try
    //        {
    //            while (_isRunning && _taskQueue.TryTake(out var task))
    //            {
    //                // Run the task on a thread pool thread. Up to this point everything
    //                // have been done on the UI/Caller thread.
    //                await Task.Run(task);
    //            }
    //        }
    //        finally
    //        {
    //            _semaphore.Release();
    //        }
    //    }

    //    public void Stop()
    //    {
    //        _isRunning = false;

    //        //This method will cause "Post" to throw InvalidOperationException.
    //        _taskQueue.CompleteAdding();
    //    }

    //    public void PostWithUiUpdate(Action backgroundTask, Action uiUpdateTask)
    //    {
    //        Post(() =>
    //        {
    //            if (_isRunning)
    //            {
    //                backgroundTask();
    //                Dispatcher.UIThread.Post(uiUpdateTask);
    //            }
    //        });
    //    }
    //}


    //Implementation with a dedicated thread:
    //public class BackgroundWorker
    //{
    //    private readonly BlockingCollection<Action> _taskQueue = new BlockingCollection<Action>();
    //    private readonly Thread _workerThread;
    //    private bool _is_running = true;

    //    public BackgroundWorker()
    //    {
    //        _workerThread = new Thread(Work);
    //        _workerThread.Start();
    //    }

    //    public void Post(Action task)
    //    {
    //        _taskQueue.Add(task);
    //    }

    //    private void Work()
    //    {
    //        //Note, BlockingCollection will never be complete until CompleteAdding is called. 
    //        foreach (var task in _taskQueue.GetConsumingEnumerable())
    //        {
    //            task();
    //        }
    //    }

    //    public void Stop()
    //    {
    //        _is_running = false;
    //        _taskQueue.CompleteAdding();

    //        //This method is used to block the calling thread until the _workerThread has finished executing. 
    //        //Warning, this can cause a deadlock if used carelessly. In this impl. we only work with two threads.
    //        //and the UI thread is always handled using the Dispatcher, so it won't be a problem.
    //        _workerThread.Join();
    //    }

    //    public void PostWithUiUpdate(Action backgroundTask, Action uiUpdateTask)
    //    {
    //        Post(() =>
    //        {
    //            if (_is_running)
    //            {
    //                backgroundTask();
    //                Dispatcher.UIThread.Post(uiUpdateTask);
    //            }
    //        });
    //    }
    //}
}
