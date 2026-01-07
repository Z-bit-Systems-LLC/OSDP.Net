using System;
using System.Threading.Tasks;

namespace OSDP.Net.Tests.Utilities
{
    /// <summary>
    /// Provided by Sinaesthetic at https://stackoverflow.com/a/52357854
    /// </summary>
    internal static class TaskEx
    {
        /// <summary>
        /// Blocks while condition is true or timeout occurs.
        /// </summary>
        /// <param name="condition">The condition that will perpetuate the block.</param>
        /// <param name="frequency">The frequency at which the condition will be check.</param>
        /// <param name="timeout">Timeout waiting for a the condition to be satisfied.</param>
        /// <exception cref="TimeoutException"></exception>
        /// <returns></returns>
        public static async Task WaitWhile(Func<bool> condition, TimeSpan frequency, TimeSpan timeout)
        {
            var waitTask = new TaskFactory().StartNew(async () =>
            {
                while (condition()) await Task.Delay(frequency);
            });

            if(waitTask != await Task.WhenAny(waitTask, Task.Delay(timeout)))
                throw new TimeoutException();
        }

        /// <summary>
        /// Blocks until condition is true or timeout occurs.
        /// </summary>
        /// <param name="condition">The break condition.</param>
        /// <param name="frequency">The frequency at which the condition will be checked.</param>
        /// <param name="timeout">Timeout waiting for a the condition to be satisfied.</param>
        /// <returns></returns>
        public static async Task WaitUntil(Func<bool> condition, TimeSpan frequency, TimeSpan timeout)
        {
            var waitTask = new TaskFactory().StartNew(async () =>
            {
                while (!condition()) await Task.Delay(frequency);
            });

            if (waitTask != await Task.WhenAny(waitTask, 
                Task.Delay(timeout))) 
                throw new TimeoutException();
        }
    }
}