using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinstonBot
{
    internal static class TaskExtensions
    {
        public static void Forget(this Task task)
        {
            task.ContinueWith(
                t => { Console.WriteLine(t.Exception); },
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
