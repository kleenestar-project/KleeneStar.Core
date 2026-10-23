using KleeneStar.Model;
using System;
using System.Threading;

namespace KleeneStar.Core.WebAutomation
{
    /// <summary>
    /// Runs the automation queue in the background: the due jobs every few seconds, the
    /// scheduled rules every minute, the housekeeping once an hour.
    /// </summary>
    /// <remarks>
    /// A tick that is still running when the next one comes is not overlapped, so a slow job
    /// never stacks workers on the same host; several hosts share the queue through its leases.
    /// A tick that fails is logged and the next one tries again - the queue is in the database,
    /// so nothing is lost between them. The worker lives as long as the application and is
    /// stopped with it.
    /// </remarks>
    public sealed class AutomationWorker : IDisposable
    {
        /// <summary>
        /// How often the due jobs are run.
        /// </summary>
        public static readonly TimeSpan JobInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// How often the scheduled rules are evaluated.
        /// </summary>
        public static readonly TimeSpan ScheduleInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// How long a finished job is kept for whoever looks at the history.
        /// </summary>
        public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

        private readonly Timer _timer;
        private int _busy;
        private DateTime _nextSchedule = DateTime.MinValue;
        private DateTime _nextHousekeeping = DateTime.MinValue;

        /// <summary>
        /// Initializes a new instance of the class and starts it.
        /// </summary>
        public AutomationWorker()
        {
            _timer = new Timer(_ => Tick(), null, JobInterval, JobInterval);
        }

        /// <summary>
        /// Runs one round: due jobs, and the schedules and housekeeping when their time has come.
        /// </summary>
        public void Tick()
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1)
            {
                return;
            }

            try
            {
                var now = DateTime.UtcNow;

                if (now >= _nextSchedule)
                {
                    _nextSchedule = now + ScheduleInterval;
                    CoreHub.AutomationManager.RunSchedules(now);
                    CoreHub.ApprovalManager.RaiseOverdue(now);
                    CoreHub.ValueCatalogManager.RefreshDue(now);
                }

                // a full batch suggests more are waiting, so the queue is drained before the
                // tick ends rather than one batch per interval
                while (CoreHub.AutomationManager.ProcessDue() >= 20)
                {
                }

                if (now >= _nextHousekeeping)
                {
                    _nextHousekeeping = now + TimeSpan.FromHours(1);
                    ModelHub.PruneAutomationJobs(now - Retention);

                    // a backup that fails must not keep the queue from running; it is logged and
                    // tried again at the next housekeeping
                    try
                    {
                        WebMaintenance.DatabaseBackups.RunDue(now);
                    }
                    catch (Exception ex)
                    {
                        CoreHub.HttpServerContext?.Log?.Exception(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                CoreHub.HttpServerContext?.Log?.Exception(ex);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        /// <summary>
        /// Stops the worker. A job it was running finishes; one it had not started stays queued.
        /// </summary>
        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
