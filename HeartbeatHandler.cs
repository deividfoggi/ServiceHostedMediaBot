namespace ServiceHostedMediaBot.Common
{
    using System;
    using System.Threading.Tasks;
    using System.Timers;
    using Microsoft.Graph.Communications.Common;
    using Microsoft.Graph.Communications.Common.Telemetry;

    public abstract class HeartbeatHandler : ObjectRootDisposable
    {
        private Timer heartbeatTimer;

        public HeartbeatHandler(TimeSpan frequency, IGraphLogger logger)
            : base(logger)
        {
            var timer = new Timer(frequency.TotalMilliseconds);
            timer.Enabled = true;
            timer.AutoReset = true;
            timer.Elapsed += this.HeartbeatDetected;
            this.heartbeatTimer = timer;
        }

        protected abstract Task HeartbeatAsync(ElapsedEventArgs args);

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            this.heartbeatTimer.Elapsed -= this.HeartbeatDetected;
            this.heartbeatTimer.Stop();
            this.heartbeatTimer.Dispose();
        }

        private void HeartbeatDetected(object snder, ElapsedEventArgs args)
        {
            var task = $"{this.GetType().FullName}.{nameof(this.HeartbeatAsync)}(args)";
            this.GraphLogger.Verbose($"Starting running tasks: " + task);
            _ = Task.Run(() => this.HeartbeatAsync(args)).ForgetAndLogExceptionAsync(this.GraphLogger, task);
        }
    }
}
