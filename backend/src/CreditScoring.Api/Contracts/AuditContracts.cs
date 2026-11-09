using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Api.Contracts;

public sealed record AuditQuery
{
    [FromQuery(Name = "from")]
    public DateTimeOffset? From { get; init; }

    [FromQuery(Name = "to")]
    public DateTimeOffset? To { get; init; }

    [FromQuery(Name = "userId")]
    public Guid? UserId { get; init; }

    [FromQuery(Name = "entity"), StringLength(AuditEntry.EntityMaxLength)]
    public string? Entity { get; init; }

    [FromQuery(Name = "entityId"), StringLength(AuditEntry.EntityIdMaxLength)]
    public string? EntityId { get; init; }

    [FromQuery(Name = "action"), EnumDataType(typeof(AuditAction))]
    public AuditAction? Action { get; init; }

    [FromQuery(Name = "page"), Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [FromQuery(Name = "pageSize"), Range(1, 100)]
    public int PageSize { get; init; } = 50;
}

public sealed record AuditEntryResponse(
    Guid Id,
    DateTimeOffset At,
    Guid? UserId,
    string? UserName,
    AuditAction Action,
    string Entity,
    string EntityId,
    string? TraceId,
    JsonElement Changes)
{
    public static AuditEntryResponse From(AuditEntry entry) => new(
        entry.Id,
        entry.At,
        entry.UserId,
        entry.UserName,
        entry.Action,
        entry.Entity,
        entry.EntityId,
        entry.TraceId,
        JsonDocument.Parse(entry.Changes).RootElement.Clone());
}
