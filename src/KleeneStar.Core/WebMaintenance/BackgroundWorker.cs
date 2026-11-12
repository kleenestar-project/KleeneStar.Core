using System;
using System.Threading;

namespace KleeneStar.Core.WebMaintenance
{
    /// <summary>
    /// The core's background work: overdue approvals and due value catalogs once a minute, a
    /// bounded slice of the large recursive operations every tick, and the database backups at
    /// the hourly housekeeping. The automation queue is not the core's - the automation plugin
    /// runs a worker of its own.
    /// </summary>
    public sealed class BackgroundWorker : IDisposable
    {
        /// <summary>
        /// How often the worker looks for work.
        /// </summary>
        public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// How often the minute-scale work runs.
        /// </summary>
        public static readonly TimeSpan ScheduleInterval = TimeSpan.FromMinutes(1);

        private readonly Timer _timer;
        private int _busy;
        private DateTime _nextSchedule = DateTime.MinValue;
        private DateTime _nextHousekeeping = DateTime.MinValue;

        /// <summary>
        /// Initializes a new instance of the class and starts the timer.
        /// </summary>
        public BackgroundWorker()
        {
            _timer = new Timer(_ => Tick(), null, TickInterval, TickInterval);
        }

        /// <summary>
        /// Runs what is due. A tick that finds the previous one still running skips; every part
        /// fails on its own, so one broken job does not keep the others from running.
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

                    Guard(() => CoreHub.ApprovalManager.RaiseOverdue(now));
                    Guard(() => CoreHub.ValueCatalogManager.RefreshDue(now));
                }

                // the recursive operation runs too large for a request (archive, delete, clone of
                // a big subtree) advance a bounded slice per tick and report their progress
                Guard(() => CoreHub.ObjectOperationManager.ProcessDue(500));

                if (now >= _nextHousekeeping)
                {
                    _nextHousekeeping = now + TimeSpan.FromHours(1);

                    // a backup that fails is logged and tried again at the next housekeeping
                    Guard(() => DatabaseBackups.RunDue(now));
                }
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        /// <summary>
        /// Stops the timer.
        /// </summary>
        public void Dispose()
        {
            _timer.Dispose();
        }

        /// <summary>
        /// Runs one part of the work and logs what it throws.
        /// </summary>
        /// <param name="work">The work.</param>
        private static void Guard(Action work)
        {
            try
            {
                work();
            }
            catch (Exception ex)
            {
                CoreHub.HttpServerContext?.Log?.Exception(ex);
            }
        }
    }
}
