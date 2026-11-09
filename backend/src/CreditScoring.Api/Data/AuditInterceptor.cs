using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CreditScoring.Api.Data;

public interface IAuditContext
{
    Actor? Actor { get; }
    string? TraceId { get; }
}

public sealed class AuditInterceptor(TimeProvider time, IAuditContext context) : SaveChangesInterceptor
{
    private const string Redacted = "[redacted]";

    private const string Personal = "[personal]";

    private static readonly HashSet<string> Secrets = [nameof(User.PasswordHash), nameof(User.SecurityStamp)];

    private static readonly HashSet<(string Entity, string Property)> PersonalData =
    [
        (nameof(Client), nameof(Client.FullName)),
        (nameof(Client), nameof(Client.DateOfBirth)),
        (nameof(Client), nameof(Client.Address)),
        (nameof(Client), nameof(Client.Phone)),
        (nameof(Client), nameof(Client.Email)),
        (nameof(Client), nameof(Client.Employer)),
        (nameof(CreditApplication), nameof(CreditApplication.ApplicantName)),
        (nameof(ApplicationNote), nameof(ApplicationNote.Text)),
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Record(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Record(DbContext? db)
    {
        if (db is null)
            throw new InvalidOperationException("SaveChanges was intercepted without a context to audit.");

        db.ChangeTracker.DetectChanges();
        var now = time.GetUtcNow();
        var entries = db.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditEntry && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList()
            .Select(e => Describe(e, now))
            .OfType<AuditEntry>()
            .ToList();
        db.Set<AuditEntry>().AddRange(entries);
    }

    private AuditEntry? Describe(EntityEntry entry, DateTimeOffset now)
    {
        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Created,
            EntityState.Modified => AuditAction.Updated,
            EntityState.Deleted => AuditAction.Deleted,
            var state => throw new InvalidOperationException($"no audit action for state {state}"),
        };

        var changes = new SortedDictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var property in entry.Properties)
        {
            if (action == AuditAction.Created && GeneratedByTheDatabase(property))
                continue;
            if (action == AuditAction.Updated && (!property.IsModified || Equals(property.OriginalValue, property.CurrentValue)))
                continue;

            var name = property.Metadata.Name;
            var entity = entry.Metadata.ClrType.Name;
            var change = new Dictionary<string, object?>();
            if (action != AuditAction.Created)
                change["before"] = Shown(entity, name, property.OriginalValue);
            if (action != AuditAction.Deleted)
                change["after"] = Shown(entity, name, property.CurrentValue);
            changes[JsonNamingPolicy.CamelCase.ConvertName(name)] = change;
        }

        if (action == AuditAction.Updated && changes.Count == 0)
            return null;

        return AuditEntry.Record(
            now,
            context.Actor,
            action,
            entry.Metadata.ClrType.Name,
            KeyOf(entry),
            context.TraceId,
            JsonSerializer.Serialize(changes, Json));
    }

    private static bool GeneratedByTheDatabase(PropertyEntry property) =>
        property.IsTemporary
        || (property.Metadata.GetDefaultValueSql() is not null && Equals(property.CurrentValue, property.Metadata.Sentinel));

    private static object? Shown(string entity, string property, object? value) =>
        Secrets.Contains(property) ? Redacted
        : PersonalData.Contains((entity, property)) ? Personal
        : value;

    private static string KeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey()
            ?? throw new InvalidOperationException($"{entry.Metadata.ClrType.Name} has no primary key to audit.");
        return string.Join(",", key.Properties.Select(p =>
            Convert.ToString(entry.Property(p.Name).CurrentValue, CultureInfo.InvariantCulture)));
    }
}
