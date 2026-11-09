using CreditScoring.Api.Auth;
using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class DevelopmentDatabaseTests
{
    private static CreditScoringDbContext Context()
    {
        var connection = new NpgsqlConnectionStringBuilder(DatabaseFixture.ConnectionString) { Database = "credit_scoring_startup_test" };
        return new CreditScoringDbContext(
            new DbContextOptionsBuilder<CreditScoringDbContext>().UseNpgsql(connection.ConnectionString).Options);
    }

    private static readonly SeedUser Officer =
        new() { UserName = "officer", DisplayName = "Ana Jovanović", Role = UserRole.Officer, Password = "pw-officer" };

    [Fact]
    public async Task An_empty_database_is_migrated_and_seeded_on_start()
    {
        await using (var db = Context())
        {
            await db.Database.EnsureDeletedAsync();
            await DevelopmentDatabase.PrepareAsync(db, [Officer], TestData.Now);
        }

        await using var check = Context();
        Assert.Empty(await check.Database.GetPendingMigrationsAsync());
        Assert.Equal("officer", (await check.Users.SingleAsync()).UserName);
        Assert.Equal("credit_lc_xgb_isotonic_v1", (await check.ScoringModels.SingleAsync(m => m.Role == ModelRole.Active)).ModelId);
    }

    [Fact]
    public async Task Preparing_again_changes_nothing()
    {
        await using (var db = Context())
        {
            await db.Database.EnsureDeletedAsync();
            await DevelopmentDatabase.PrepareAsync(db, [Officer], TestData.Now);
        }
        await using (var db = Context())
            await DevelopmentDatabase.PrepareAsync(db, [Officer], TestData.Now);

        await using var check = Context();
        Assert.Equal(1, await check.Users.CountAsync());
        Assert.Equal(1, await check.ScoringModels.CountAsync(m => m.Role == ModelRole.Active));
    }
}
