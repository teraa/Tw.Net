using System;
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
                catch (Exception ex) when (ex.InnerException is not null)
                {
                    throw ex.InnerException;
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
                catch (Exception ex) when (ex.InnerException is not null)
                {
                    throw ex.InnerException;
                }
            }
        }
    }
}
