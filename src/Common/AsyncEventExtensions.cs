using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Twitch
{
    public static class AsyncEventExtensions
    {
        public static Task InvokeAsync(this Func<Task> eventHandler)
            => InvokePrivateAsync(eventHandler, null);

        public static Task InvokeAsync<T>(this Func<T, Task> eventHandler, T arg) where T : class
            => InvokePrivateAsync(eventHandler, new[] { arg });

        private static async Task InvokePrivateAsync(MulticastDelegate d, object?[]? parameters)
        {
            var list = d.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                var sub = list[i];
                try
                {
                    var task = (Task)sub.Method.Invoke(sub.Target, parameters)!;
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
