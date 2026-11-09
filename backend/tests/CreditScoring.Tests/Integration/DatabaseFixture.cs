using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5433;Database=credit_scoring_test;Username=credit_scoring;Password=dev_only_password";

    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("CREDITSCORING_TEST_DB") ?? DefaultConnectionString;

    public const string TraceId = "test-trace";

    public CreditScoringDbContext CreateContext(Actor? actor = null) =>
        new(new DbContextOptionsBuilder<CreditScoringDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new AuditInterceptor(TimeProvider.System, new FixedAuditContext(actor, TraceId)))
            .Options);

    public User Officer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        Officer = User.Create("officer", "Olga Officer", UserRole.Officer, TestData.Now);
        db.Users.Add(Officer);
        await db.SaveChangesAsync();
    }

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.AuditEntries.ExecuteDeleteAsync();
        await db.Applications.ExecuteDeleteAsync();
        await db.Clients.ExecuteDeleteAsync();
        await db.ModelEvents.ExecuteDeleteAsync();
        await db.ScoringModels.Where(m => m.ModelId != TestData.ModelId).ExecuteDeleteAsync();
        await db.ScoringModels.ExecuteUpdateAsync(m => m.SetProperty(x => x.Role, ModelRole.Active));
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
