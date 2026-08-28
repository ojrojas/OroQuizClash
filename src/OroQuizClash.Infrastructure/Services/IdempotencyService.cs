using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using OroQuizClash.Domain.Audit;
using OroQuizClash.Infrastructure.Persistence;

namespace OroQuizClash.Infrastructure.Services;

public interface IIdempotencyService
{
    Task<(bool isDuplicate, string? cachedResponse, bool isReplay)> CheckAsync(string key, string actorId, string requestHash, CancellationToken cancellationToken);
    Task StoreAsync(string key, string actorId, string requestHash, string responseJson, CancellationToken cancellationToken);
}

public sealed class IdempotencyService(
    OroQuizClashDbContext dbContext,
    IConfiguration configuration,
    ILogger<IdempotencyService> logger) : IIdempotencyService
{
    private TimeSpan Window => TimeSpan.FromHours(configuration.GetValue("Security:IdempotencyWindowHours", 24));

    public async Task<(bool isDuplicate, string? cachedResponse, bool isReplay)> CheckAsync(string key, string actorId, string requestHash, CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key && r.ActorId == actorId, cancellationToken);

        if (record is null) return (false, null, false);

        if (DateTimeOffset.UtcNow - record.CreatedAt > Window)
        {
            dbContext.IdempotencyRecords.Remove(record);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (false, null, false);
        }

        if (record.ResponseHash == requestHash)
        {
            return (true, record.Response, false);
        }

        return (false, null, true);
    }

    public async Task StoreAsync(string key, string actorId, string requestHash, string responseJson, CancellationToken cancellationToken)
    {
        try
        {
            var record = IdempotencyRecord.Create(key, actorId, DateTimeOffset.UtcNow, requestHash, responseJson);
            dbContext.IdempotencyRecords.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to store idempotency record for key {Key}", key);
        }
    }

    public static string ComputeHash(object request)
    {
        var json = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
