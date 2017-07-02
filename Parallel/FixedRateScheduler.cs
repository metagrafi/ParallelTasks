using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Parallel
{
    class ScheduledGroup
    {
        public LinkedList<Task> tasks { get; set; }
        public DateTime time { get; set; }
    }
    // Provides a task scheduler that ensures a maximum concurrency level while 
    // running on top of the thread pool.
    public class FixedRateScheduler : TaskScheduler, IDisposable
    {
        // Indicates whether the current thread is processing work items.
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;

        // The list of tasks to be executed 
        private LinkedList<Task> _tasks = new LinkedList<Task>(); // protected by lock(_tasks)

        // The list of groups of tasks to be executed 
        private readonly LinkedList<ScheduledGroup> _scheduledTasks = new LinkedList<ScheduledGroup>(); // protected by lock(_scheduledTasks)

        // The maximum concurrency level allowed by this scheduler. 
        private readonly int _maxDegreeOfParallelism;

        // Indicates whether the scheduler is currently processing work items. 
        private int _delegatesQueuedOrRunning = 0;

        //Scheduling parameters
        private TimeSpan? _period = null;

        private readonly int? _numberOfTasks;

        private readonly int? _totalTasks;

        private System.Timers.Timer _timer;


        // Creates a new instance with the specified degree of parallelism. 
        public FixedRateScheduler(int? maxDegreeOfParallelism, TimeSpan? period, int? groupSize, int? totalTasks)
        {
            _period = period;
            _numberOfTasks = groupSize;
            _totalTasks = totalTasks;
            if (_period != null)
            {
                if (_numberOfTasks > _totalTasks) _numberOfTasks = _totalTasks;
                _timer = new System.Timers.Timer(_period.Value.TotalMilliseconds);
                _timer.Elapsed +=
                    ((object o, ElapsedEventArgs args) =>
                    {
                        if (_scheduledTasks.Count > 0)
                        {
                            lock (_scheduledTasks)
                            {
                                _tasks = _scheduledTasks.First.Value.tasks;
                                _scheduledTasks.RemoveFirst();
                                inlineTasks();
                            }
                        }

                    });
                _timer.Start();
            }

            if (maxDegreeOfParallelism.HasValue && maxDegreeOfParallelism.Value == 0) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
            _maxDegreeOfParallelism = maxDegreeOfParallelism ?? -1;
        }

        // Queues a task to the scheduler. 
        protected sealed override void QueueTask(Task task)
        {

            lock (_tasks)
            {
                _tasks.AddLast(task);

                if (_period == null)
                {

                    inlineTasks();
                }
                else
                {
                    if (_tasks.Count % _numberOfTasks.Value == 0 ||
                                _totalTasks.Value - (_scheduledTasks.Count * _numberOfTasks.Value) == _tasks.Count)
                    {
                        lock (_scheduledTasks)
                        {
                            var time = DateTime.Now + _period.Value;
                            var scheduledGroup = new ScheduledGroup
                            {
                                time = time,
                                tasks = new LinkedList<Task>()
                            };
                            foreach (var t in _tasks)
                            {
                                scheduledGroup.tasks.AddLast(t);

                            }
                            _scheduledTasks.AddLast(scheduledGroup);
                            _tasks.Clear();
                        }
                    }

                }

            }

        }


        //If there aren't enough 
        // delegates currently queued or running to process tasks, schedule another. 
        private void inlineTasks()
        {
            if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism || _maxDegreeOfParallelism < 0)
            {
                ++_delegatesQueuedOrRunning;
                NotifyThreadPoolOfPendingWork();
            }

        }

        // Inform the ThreadPool that there's work to be executed for this scheduler. 
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Note that the current thread is now processing work items.
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;

                try
                {
                    // Process all available items in the queue.
                    while (true)
                    {
                        Task item;
                        lock (_tasks)
                        {
                            // When there are no more items to be processed,
                            // note that we're done processing, and get out.
                            if (_tasks.Count == 0) break;
                            // Get the next item from the queue
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue
                        base.TryExecuteTask(item);
                        --_delegatesQueuedOrRunning;

                    }
                }
                // We're done processing items on the current thread
                finally
                {
                    _currentThreadIsProcessingItems = false;
                }
            }, null);
        }

        // Attempts to execute the specified task on the current thread. 
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            if (!_currentThreadIsProcessingItems) return false;

            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued)
                // Try to run the task. 
                if (TryDequeue(task))
                    return base.TryExecuteTask(task);
                else
                    return false;
            else
                return base.TryExecuteTask(task);
        }

        // Attempt to remove a previously scheduled task from the scheduler. 
        protected sealed override bool TryDequeue(Task task)
        {
            lock (_tasks) return _tasks.Remove(task);
        }

        // Gets the maximum concurrency level supported by this scheduler. 
        public sealed override int MaximumConcurrencyLevel { get { return _maxDegreeOfParallelism; } }

        // Gets an enumerable of the tasks currently scheduled on this scheduler. 
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken) return _tasks;
                else
                    throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_tasks);
            }
        }

        public void Dispose()
        {
            if (_timer != null)
                _timer.Stop();
        }
    }
}
