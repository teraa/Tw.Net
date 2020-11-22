using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Twitch
{
    internal static class AsyncEventExtensions
    {
        public static Task InvokeAsync(this Func<Task> eventHandler)
            => InvokePrivateAsync(eventHandler, null);

        public static Task InvokeAsync<T>(this Func<T, Task> eventHandler, T? arg)
            where T : class
            => InvokePrivateAsync(eventHandler, new[] { arg });

        public static Task InvokeAsync<T1, T2>(this Func<T1, T2, Task> eventHandler, T1? arg1, T2? arg2)
            where T1 : class
            where T2 : class
            => InvokePrivateAsync(eventHandler, new object?[] { arg1, arg2 });

        private static async Task InvokePrivateAsync(MulticastDelegate d, object?[]? parameters)
        {
            if (d is null)
                throw new ArgumentNullException(nameof(d));

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
