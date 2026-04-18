using System.Collections.Concurrent;

namespace Platform.Core.Abstractions;

public sealed record AppEvent<TPayload>(
    string Name,
    TPayload Payload,
    DateTimeOffset OccurredAt,
    string? TraceId = null);

public interface IAppEventBus
{
    Task PublishAsync<TPayload>(AppEvent<TPayload> evt, CancellationToken cancellationToken = default);

    IDisposable Subscribe<TPayload>(string eventName, Func<AppEvent<TPayload>, CancellationToken, Task> handler);
}

