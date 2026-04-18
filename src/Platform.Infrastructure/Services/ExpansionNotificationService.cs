namespace Platform.Infrastructure.Services;

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.Core.Abstractions;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.Net.Mail;

public sealed class NotificationService(
    AppDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<EmailChannelOptions> emailOptions,
    IOptions<SmsChannelOptions> smsOptions)
{
    public async Task<NotificationMessageEntity> SendInboxAsync(string title, string content, string? receiver, CancellationToken cancellationToken = default)
    {
        var message = new NotificationMessageEntity
        {
            NotificationMessageId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            Channel = "Inbox",
            Receiver = receiver,
            Title = title,
            Content = content,
            Status = "Sent",
            CreatedAt = DateTimeOffset.UtcNow,
            SentAt = DateTimeOffset.UtcNow
        };
        dbContext.NotificationMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task<NotificationMessageEntity> SendWebhookAsync(string callbackUrl, string title, string content, CancellationToken cancellationToken = default)
    {
        var message = new NotificationMessageEntity
        {
            NotificationMessageId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            Channel = "Webhook",
            Receiver = callbackUrl,
            Title = title,
            Content = content,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.NotificationMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        await TrySendWebhookAsync(message, cancellationToken);
        return message;
    }

    public async Task<NotificationMessageEntity> SendEmailAsync(string receiverEmail, string title, string content, CancellationToken cancellationToken = default)
    {
        var message = new NotificationMessageEntity
        {
            NotificationMessageId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            Channel = "Email",
            Receiver = receiverEmail,
            Title = title,
            Content = content,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.NotificationMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        await TrySendEmailAsync(message, cancellationToken);
        return message;
    }

    public async Task<NotificationMessageEntity> SendSmsAsync(string receiverPhone, string title, string content, CancellationToken cancellationToken = default)
    {
        var message = new NotificationMessageEntity
        {
            NotificationMessageId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            Channel = "Sms",
            Receiver = receiverPhone,
            Title = title,
            Content = content,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.NotificationMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        await TrySendSmsAsync(message, cancellationToken);
        return message;
    }

    public async Task<IReadOnlyCollection<NotificationMessageEntity>> GetRecentAsync(CancellationToken cancellationToken = default) =>
        await dbContext.NotificationMessages
            .AsNoTracking()
            .Where(x => x.TenantId == tenantContextAccessor.TenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

    public async Task TrySendWebhookAsync(NotificationMessageEntity message, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(message.Channel, "Webhook", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(message.Receiver))
        {
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient(nameof(NotificationService));
            var body = JsonSerializer.Serialize(new { message.NotificationMessageId, message.Title, message.Content, message.CreatedAt });
            using var response = await SendWithRetryAsync(
                ct => client.PostAsync(message.Receiver, new StringContent(body, Encoding.UTF8, "application/json"), ct),
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                message.Status = "Sent";
                message.SentAt = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            else
            {
                message.Status = "Failed";
                message.RetryCount += 1;
                message.Error = $"Webhook returned {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            message.Status = "Failed";
            message.RetryCount += 1;
            message.Error = ex.Message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task TrySendEmailAsync(NotificationMessageEntity message, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(message.Channel, "Email", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(message.Receiver))
        {
            return;
        }

        try
        {
            var options = emailOptions.Value;
            if (!options.Enabled)
            {
                throw new InvalidOperationException("Email channel disabled.");
            }

            using var smtp = new SmtpClient(options.Host, options.Port)
            {
                EnableSsl = options.EnableSsl,
                Credentials = string.IsNullOrWhiteSpace(options.Username)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(options.Username, options.Password)
            };
            using var mail = new MailMessage(options.FromAddress, message.Receiver, message.Title, message.Content);
            await smtp.SendMailAsync(mail, cancellationToken);

            message.Status = "Sent";
            message.SentAt = DateTimeOffset.UtcNow;
            message.Error = null;
        }
        catch (Exception ex)
        {
            message.Status = "Failed";
            message.RetryCount += 1;
            message.Error = ex.Message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task TrySendSmsAsync(NotificationMessageEntity message, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(message.Channel, "Sms", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(message.Receiver))
        {
            return;
        }

        try
        {
            var options = smsOptions.Value;
            if (!options.Enabled)
            {
                throw new InvalidOperationException("Sms channel disabled.");
            }
            if (string.IsNullOrWhiteSpace(options.ProviderUrl))
            {
                throw new InvalidOperationException("Sms provider url is required.");
            }

            var client = httpClientFactory.CreateClient(nameof(NotificationService));
            var body = JsonSerializer.Serialize(new
            {
                to = message.Receiver,
                sender = options.SenderId,
                title = message.Title,
                content = message.Content
            });
            using var response = await SendWithRetryAsync(
                ct => SendSmsRequestAsync(client, options, body, ct),
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                message.Status = "Sent";
                message.SentAt = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            else
            {
                message.Status = "Failed";
                message.RetryCount += 1;
                message.Error = $"Sms provider returned {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            message.Status = "Failed";
            message.RetryCount += 1;
            message.Error = ex.Message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> sender,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        var delay = TimeSpan.FromMilliseconds(300);
        Exception? lastException = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await sender(cancellationToken);
                if ((int)response.StatusCode >= 500 && attempt < maxAttempts)
                {
                    response.Dispose();
                    await Task.Delay(delay, cancellationToken);
                    delay = delay * 2;
                    continue;
                }

                return response;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                await Task.Delay(delay, cancellationToken);
                delay = delay * 2;
            }
        }

        throw lastException ?? new InvalidOperationException("HTTP send failed after retries.");
    }

    private static async Task<HttpResponseMessage> SendSmsRequestAsync(
        HttpClient client,
        SmsChannelOptions options,
        string body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.ProviderUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", options.ApiKey);
        }

        return await client.SendAsync(request, cancellationToken);
    }
}
