

namespace ServiceHostedMediaBot.Logging
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Graph.Communications.Common.Telemetry;

    public class SampleObserver : IObserver<LogEvent>, IDisposable
    {
        private static readonly int MaxLogCount = 5000;

        private IDisposable subscription;

        private LinkedList<string> logs = new LinkedList<string>();

        private object lockLogs = new object();

        private ILogEventFormatter formatter = new CommsLogEventFormatter();

        public SampleObserver(IGraphLogger logger)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) => logger.Error(e.ExceptionObject as Exception, $"Unhandled exception");
            TaskScheduler.UnobservedTaskException += (_, e) => logger.Error(e.Exception, "Unobserved task exception");

            this.subscription = logger.Subscribe(this);
        }

        public string GetLogs(int skip = 0, int take = int.MaxValue)
        {
            lock (this.lockLogs)
            {
                skip = skip < 0 ? Math.Max(0, this.logs.Count + skip) : skip;
                var filteredLogs = this.logs
                    .Skip(skip)
                    .Take(take);
                return string.Join(Environment.NewLine, filteredLogs);
            }
        }

        public string GetLogs(string filter, int skip = 0, int take = int.MaxValue)
        {
            lock (this.lockLogs)
            {
                skip = skip < 0 ? Math.Max(0, this.logs.Count + skip) : skip;
                var filteredLogs = this.logs
                    .Where(log => log.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Skip(skip)
                    .Take(take);
                return string.Join(Environment.NewLine, filteredLogs);
            }
        }

        public void OnNext(LogEvent logEvent)
        {
            if (logEvent.EventType == LogEventType.Metric)
            {
                return;
            }

            if (logEvent.EventType != LogEventType.HttpTrace)
            {
                if (logEvent.Level != TraceLevel.Error && logEvent.Level != TraceLevel.Warning)
                {
                    return;
                }
            }

            var logString = this.formatter.Format(logEvent);
            lock (this.lockLogs)
            {
                this.logs.AddFirst(logString);
                if (this.logs.Count > MaxLogCount)
                {
                    this.logs.RemoveLast();
                }
            }
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        public void Dispose()
        {
            lock (this.lockLogs)
            {
                this.logs?.Clear();
                this.logs = null;
            }

            this.subscription?.Dispose();
            this.subscription = null;
            this.formatter = null;
        }
    }
}