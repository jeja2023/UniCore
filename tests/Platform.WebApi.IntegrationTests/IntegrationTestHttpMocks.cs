using System.Collections.Concurrent;
using System.Net;

namespace Platform.WebApi.IntegrationTests;

public sealed record CallbackRecord(string Url, string Method, string Body, IReadOnlyDictionary<string, string> Headers);

public sealed class CallbackRecorder
{
    private readonly ConcurrentQueue<CallbackRecord> records = new();

    public void Add(CallbackRecord record) => records.Enqueue(record);

    public bool TryDequeue(out CallbackRecord? record)
    {
        var ok = records.TryDequeue(out var value);
        record = value;
        return ok;
    }
}

public sealed class CallbackHttpClientFactory(
    CallbackRecorder recorder,
    HttpStatusCode responseStatusCode = HttpStatusCode.OK) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new CallbackCaptureHandler(recorder, responseStatusCode));
}

public sealed class CallbackCaptureHandler(
    CallbackRecorder recorder,
    HttpStatusCode responseStatusCode) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(
            x => x.Key,
            x => string.Join(",", x.Value),
            StringComparer.OrdinalIgnoreCase);
        recorder.Add(new CallbackRecord(
            request.RequestUri?.ToString() ?? string.Empty,
            request.Method.Method,
            body,
            headers));

        return new HttpResponseMessage(responseStatusCode);
    }
}
