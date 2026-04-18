using System.Collections.Concurrent;
using Platform.Core.Abstractions;

namespace Platform.Infrastructure.Services;

public sealed class InMemoryAppEventBus : IAppEventBus
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Func<object, CancellationToken, Task>>> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public IDisposable Subscribe<TPayload>(string eventName, Func<AppEvent<TPayload>, CancellationToken, Task> handler)
    {
        var id = Guid.NewGuid();
        var group = _handlers.GetOrAdd(eventName, _ => new ConcurrentDictionary<Guid, Func<object, CancellationToken, Task>>());
        group[id] = async (evt, ct) => await handler((AppEvent<TPayload>)evt, ct);

        return new Subscription(() =>
        {
            if (_handlers.TryGetValue(eventName, out var g))
            {
                g.TryRemove(id, out _);
            }
        });
    }

    public Task PublishAsync<TPayload>(AppEvent<TPayload> evt, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(evt.Name, out var group) || group.Count == 0)
        {
            return Task.CompletedTask;
        }

        var tasks = group.Values.Select(h => h(evt, cancellationToken));
        return Task.WhenAll(tasks);
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }
}

