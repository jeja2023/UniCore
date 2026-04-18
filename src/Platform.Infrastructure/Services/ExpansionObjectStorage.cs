using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Core.Abstractions;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Platform.Core.Common;

namespace Platform.Infrastructure.Services;

public sealed class ObjectStorageOptions
{
    public const string Section = "ObjectStorage";
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "unicore-files";
    public bool UseSsl { get; set; }
    public bool ForcePathStyle { get; set; } = true;
}

public sealed class S3FileStorage(ObjectStorageOptions options) : IFileStorage
{
    private readonly AmazonS3Client _client = BuildClient(options);
    private readonly string _bucketName = options.BucketName;

    public async Task<StoredFileInfo> SaveAsync(
        Stream content,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var fileId = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        await EnsureBucketExistsAsync(cancellationToken);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = fileId,
            InputStream = content,
            ContentType = contentType ?? "application/octet-stream",
            AutoCloseStream = false
        }, cancellationToken);

        return new StoredFileInfo(fileId, fileName, contentType ?? "application/octet-stream", content.CanSeek ? content.Length : 0, DateTimeOffset.UtcNow);
    }

    public async Task<StoredFileContent?> ReadAsync(string fileId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucketName, fileId, cancellationToken);
            await using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, cancellationToken);
            return new StoredFileContent(fileId, fileId, response.Headers.ContentType ?? "application/octet-stream", ms.ToArray());
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_client, _bucketName);
        if (!exists)
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName }, cancellationToken);
        }
    }

    private static AmazonS3Client BuildClient(ObjectStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new InvalidOperationException("ObjectStorage:Endpoint 不能为空。");
        }

        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = options.ForcePathStyle,
            UseHttp = !options.UseSsl
        };
        var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
        return new AmazonS3Client(credentials, config);
    }
}

public sealed class TrackingFileStorage(
    IFileStorage innerStorage,
    IServiceScopeFactory scopeFactory,
    ITenantContextAccessor tenantContextAccessor,
    string provider,
    string bucket) : IFileStorage
{
    public async Task<StoredFileInfo> SaveAsync(Stream content, string fileName, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var stored = await innerStorage.SaveAsync(content, fileName, contentType, cancellationToken);
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.StoredFileObjects.Add(new StoredFileObjectEntity
        {
            StoredFileObjectId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            FileId = stored.FileId,
            Provider = provider,
            Bucket = bucket,
            ObjectKey = stored.FileId,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            Length = stored.Length,
            StoredAt = stored.StoredAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return stored;
    }

    public Task<StoredFileContent?> ReadAsync(string fileId, CancellationToken cancellationToken = default) =>
        innerStorage.ReadAsync(fileId, cancellationToken);
}
