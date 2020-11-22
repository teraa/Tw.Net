using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
namespace Twitch
{
    public static class AsyncEventExtensions
    {
        public static async Task InvokeAsync(this Func<Task> eventHandler)
        {
            var list = eventHandler.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                var sub = list[i];
                try
                {
                    var task = (Task)sub.Method.Invoke(sub.Target, null)!;
                    await task.ConfigureAwait(false);
                }
                catch (TargetInvocationException ex)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
                }
            }
        }

        public static async Task InvokeAsync<T>(this Func<T, Task> eventHandler, T arg)
            where T : class
        {
            var list = eventHandler.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                var sub = list[i];
                try
                {
                    var task = (Task)sub.Method.Invoke(sub.Target, new[] { arg })!;
                    await task.ConfigureAwait(false);
                }
                catch (TargetInvocationException ex)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
                }
            }
        }
    }
}
