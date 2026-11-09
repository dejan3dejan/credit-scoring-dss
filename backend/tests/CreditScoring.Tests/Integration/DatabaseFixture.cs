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

    public CreditScoringDbContext CreateContext(Actor? actor = null, Guid? branchScope = null) =>
        new(new DbContextOptionsBuilder<CreditScoringDbContext>()
                .UseNpgsql(ConnectionString)
                .AddInterceptors(new AuditInterceptor(TimeProvider.System, new FixedAuditContext(actor, TraceId)))
                .Options,
            new FixedBranchScope(branchScope));

    public Guid HeadOfficeId { get; private set; }
    public Guid OtherBranchId { get; private set; }
    public User OtherOfficer { get; private set; } = null!;

    public User Officer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        HeadOfficeId = (await db.Branches.SingleAsync(b => b.Code == "HQ")).Id;
        var other = Branch.Create("OTHER", "Other branch", null, TestData.Now);
        db.Branches.Add(other);
        OtherBranchId = other.Id;

        Officer = User.Create("officer", "Olga Officer", UserRole.Officer, TestData.Now, HeadOfficeId);
        OtherOfficer = User.Create("other.officer", "Oskar Other", UserRole.Officer, TestData.Now, OtherBranchId);
        db.Users.AddRange(Officer, OtherOfficer);
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
