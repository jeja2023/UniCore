namespace Platform.Core.Abstractions;

public interface IAppCache
{
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public sealed record StoredFileInfo(string FileId, string FileName, string ContentType, long Length, DateTimeOffset StoredAt);

public sealed record StoredFileContent(string FileId, string FileName, string ContentType, byte[] Content);

public interface IFileStorage
{
    Task<StoredFileInfo> SaveAsync(
        Stream content,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    Task<StoredFileContent?> ReadAsync(string fileId, CancellationToken cancellationToken = default);
}

public interface ITenantContextAccessor
{
    string TenantId { get; }
}
