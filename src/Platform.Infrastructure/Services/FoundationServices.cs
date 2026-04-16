using Platform.Core.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Platform.Infrastructure.Services;

public sealed class InMemoryAppCache : IAppCache
{
    private readonly ConcurrentDictionary<string, CacheItem> cache = new(StringComparer.Ordinal);

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var expiresAt = ttl.HasValue ? DateTimeOffset.UtcNow.Add(ttl.Value) : (DateTimeOffset?)null;
        cache[key] = new CacheItem(value, expiresAt);
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!cache.TryGetValue(key, out var item))
        {
            return Task.FromResult(default(T));
        }

        if (item.ExpiresAt is not null && item.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            cache.TryRemove(key, out _);
            return Task.FromResult(default(T));
        }

        return item.Value is T typed ? Task.FromResult<T?>(typed) : Task.FromResult(default(T));
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private sealed record CacheItem(object? Value, DateTimeOffset? ExpiresAt);
}

public sealed class RedisCacheOptions
{
    public const string Section = "RedisCache";

    public bool Enabled { get; set; }

    public string ConnectionString { get; set; } = "localhost:6379";
}

public sealed class DistributedAppCache(IDistributedCache distributedCache) : IAppCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var options = new DistributedCacheEntryOptions();
        if (ttl.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = ttl.Value;
        }

        await distributedCache.SetAsync(key, bytes, options, cancellationToken);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var bytes = await distributedCache.GetAsync(key, cancellationToken);
        if (bytes is null || bytes.Length == 0)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        distributedCache.RemoveAsync(key, cancellationToken);
}

public sealed class LocalFileStorageOptions
{
    public const string Section = "LocalFileStorage";

    public string RootPath { get; set; } = "App_Data/uploads";
}

public sealed class LocalFileStorage(LocalFileStorageOptions options) : IFileStorage
{
    public async Task<StoredFileInfo> SaveAsync(
        Stream content,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var id = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var root = ResolveRootPath(options.RootPath);
        Directory.CreateDirectory(root);
        var fullPath = Path.Combine(root, id);
        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, cancellationToken);
        var info = new FileInfo(fullPath);
        return new StoredFileInfo(id, fileName, contentType ?? "application/octet-stream", info.Length, DateTimeOffset.UtcNow);
    }

    public async Task<StoredFileContent?> ReadAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId) || fileId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        var root = ResolveRootPath(options.RootPath);
        var fullPath = Path.Combine(root, fileId);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        return new StoredFileContent(fileId, fileId, "application/octet-stream", bytes);
    }

    private static string ResolveRootPath(string configuredRoot)
    {
        if (Path.IsPathRooted(configuredRoot))
        {
            return configuredRoot;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredRoot));
    }
}
