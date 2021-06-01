namespace ServiceHostedMediaBot.Utils
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public static class TaskExtensions
    {
        public static async Task ValidateAsync(this Task task, TimeSpan delay, string message = null)
        {
            var delayTask = Task.Delay(delay);
            var completedTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
            //Assert.AreEqual(task, completedTask, message ?? "Validating Task timed out.");
        }

        public static async Task<T> ValidateAsync<T>(this Task<T> task, TimeSpan delay, string message = null)
        {
            var delayTask = Task.Delay(delay);
            var completedTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
            if (completedTask == delayTask)
            {
                Assert.Fail(message ?? $"Validating Task<{typeof(T).Name}> timed out.");
            }

            return await task.ConfigureAwait(false);
        }

        public static async void ForgetAndLogException(
            this Task task,
            IGraphLogger logger,
            string description = null,
            [CallerMemberName] string memberName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                description = string.IsNullOrWhiteSpace(description)
                    ? "Exception while executing task."
                    : description;

                logger.Error(
                    ex,
                    description,
                    memberName: memberName,
                    filePath: filePath,
                    lineNumber: lineNumber);
            }
        }
    }
}
