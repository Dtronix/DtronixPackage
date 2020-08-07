using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DtronixPackage.Tests
{
    public static class TaskExtensions
    {
        public static async Task Timeout(this Task task, TimeSpan timeout)
        {
            var timeoutTask = Task.Delay(timeout);

            var completed = await Task.WhenAny(task, timeoutTask);
            if (timeoutTask.Equals(completed))
            {
                throw new TimeoutException($"Task exceeded timeout of {timeout.TotalMilliseconds}ms.");
            }
        }

        public static Task Timeout(this Task task, int timeout)
        {
            return Timeout(task, new TimeSpan(0, 0, 0, 0, timeout));
        } 
        
        public static async Task<T> Timeout<T>(this Task<T> task, TimeSpan timeout)
        {
            var timeoutTask = Task.Delay(timeout);

            var completed = await Task.WhenAny(task, timeoutTask);
            if (timeoutTask.Equals(completed))
            {
                throw new TimeoutException($"Task exceeded timeout of {timeout.TotalMilliseconds}ms.");
            }

            return await task;
        }

        public static Task<T> Timeout<T>(this Task<T> task, int timeout)
        {
            return Timeout(task, new TimeSpan(0, 0, 0, 0, timeout));
        }
    }
}
