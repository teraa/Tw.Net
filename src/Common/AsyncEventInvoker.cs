using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Twitch
{
    internal class AsyncEventInvoker
    {
        private readonly TimeSpan _warnTimeout;
        private readonly ILogger? _logger;

        public AsyncEventInvoker(TimeSpan warnTimeout, ILogger? logger)
        {
            _warnTimeout = warnTimeout;
            _logger = logger;
        }

        public Task InvokeAsync(Func<Task>? eventFunc, string eventName)
            => InvokeInternalAsync(eventFunc, eventName, null);

        public Task InvokeAsync<T>(Func<T, Task>? eventFunc, string eventName, T arg)
            => InvokeInternalAsync(eventFunc, eventName, new object?[] { arg });

        public Task InvokeAsync<T1, T2>(Func<T1, T2, Task>? eventFunc, string eventName, T1 arg1, T2 arg2)
            => InvokeInternalAsync(eventFunc, eventName, new object?[] { arg1, arg2 });


        private async Task InvokeInternalAsync(MulticastDelegate? eventDelegate, string eventName, object?[]? parameters)
        {
            try
            {
                var ev = eventDelegate;
                if (ev is not null)
                {
                    var warnTask = Task.Delay(_warnTimeout);
                    var evTask = ev.InvokeAsync(parameters);
                    var task = await Task.WhenAny(warnTask, evTask).ConfigureAwait(false);

                    if (task == warnTask)
                    {
                        _logger?.LogWarning($"A {eventName} handler is taking too long to execute");
                    }

                    await evTask.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {   
                _logger?.LogError(ex, $"Exception in a {eventName} handler");
            }
        }
    }
}
