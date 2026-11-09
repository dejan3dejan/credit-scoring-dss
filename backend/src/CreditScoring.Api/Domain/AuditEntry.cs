namespace CreditScoring.Api.Domain;

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
}

public class AuditEntry
{
    public const int EntityMaxLength = 64;
    public const int EntityIdMaxLength = 128;
    public const int TraceIdMaxLength = 128;

    private AuditEntry() { }

    public Guid Id { get; private set; }
    public DateTimeOffset At { get; private set; }
    public Guid? UserId { get; private set; }
    public string? UserName { get; private set; }
    public AuditAction Action { get; private set; }
    public string Entity { get; private set; } = "";
    public string EntityId { get; private set; } = "";
    public string? TraceId { get; private set; }
    public string Changes { get; private set; } = "{}";

    public static AuditEntry Record(
        DateTimeOffset at,
        Actor? by,
        AuditAction action,
        string entity,
        string entityId,
        string? traceId,
        string changes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(changes);
        if (!Enum.IsDefined(action))
            throw new ArgumentOutOfRangeException(nameof(action), action, "unknown audit action");

        return new AuditEntry
        {
            Id = Guid.CreateVersion7(at),
            At = at,
            UserId = by?.UserId,
            UserName = by?.Name,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            TraceId = traceId,
            Changes = changes,
        };
    }
}
