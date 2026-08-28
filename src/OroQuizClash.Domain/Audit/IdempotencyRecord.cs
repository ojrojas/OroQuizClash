using BuildingBlocks.Kernel.Domain.Entities;

namespace OroQuizClash.Domain.Audit;

public sealed class IdempotencyRecord : AggregateRoot<Guid>
{
    public string Key { get; private set; } = string.Empty;
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public string ResponseHash { get; private set; } = string.Empty;
    public string Response { get; private set; } = string.Empty;

    private IdempotencyRecord() { }

    private IdempotencyRecord(Guid id, string key, string actorId, DateTimeOffset createdAt, string responseHash, string response) : base(id)
    {
        Key = key;
        ActorId = actorId;
        CreatedAt = createdAt;
        ResponseHash = responseHash;
        Response = response;
    }

    public static IdempotencyRecord Create(string key, string actorId, DateTimeOffset createdAt, string responseHash, string response)
    {
        return new IdempotencyRecord(Guid.NewGuid(), key, actorId, createdAt, responseHash, response);
    }
}
