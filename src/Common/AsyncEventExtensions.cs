using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Twitch
{
    public static class AsyncEventExtensions
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

        public static async ValueTask<T> GetResponseAsync<T>(this Func<T, ValueTask>? evt, Func<T, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sourceTask = source.Task;
            Task winnerTask;
            evt += Handler;
            try
            {
                winnerTask = await Task.WhenAny(Task.Delay(timeout, cancellationToken), sourceTask).ConfigureAwait(false);
            }
            finally
            {
                evt -= Handler;
            }

            if (winnerTask == sourceTask)
                return await sourceTask.ConfigureAwait(false);

            throw new TimeoutException($"Response not received within {timeout}.");

            ValueTask Handler(T message)
            {
                if (predicate(message))
                    source.TrySetResult(message);

                return ValueTask.CompletedTask;
            }
        }
    }
}
