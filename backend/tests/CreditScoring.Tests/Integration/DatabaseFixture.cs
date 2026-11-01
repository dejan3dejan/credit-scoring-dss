using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5433;Database=credit_scoring_test;Username=credit_scoring;Password=dev_only_password";

    private readonly string connectionString =
        Environment.GetEnvironmentVariable("CREDITSCORING_TEST_DB") ?? DefaultConnectionString;

    public CreditScoringDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CreditScoringDbContext>().UseNpgsql(connectionString).Options);

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
        await db.Applications.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
