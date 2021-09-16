using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Twitch
{
    internal static class AsyncEventExtensions
    {
        public static async Task InvokeAsync(this MulticastDelegate eventDelegate, object?[]? parameters)
        {
            if (eventDelegate is null)
                throw new ArgumentNullException(nameof(eventDelegate));

            var invocationList = eventDelegate.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                var sub = invocationList[i];
                try
                {
                    await ((Task)sub.Method.Invoke(sub.Target, parameters)!).ConfigureAwait(false);
                }
                catch (TargetInvocationException ex)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
                }
            }
        }
    }
}
