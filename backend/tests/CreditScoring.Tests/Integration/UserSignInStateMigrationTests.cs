using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class UserSignInStateMigrationTests
{
    private static CreditScoringDbContext Context()
    {
        var connection = new NpgsqlConnectionStringBuilder(DatabaseFixture.ConnectionString) { Database = "credit_scoring_signin_migration_test" };
        return new CreditScoringDbContext(
            new DbContextOptionsBuilder<CreditScoringDbContext>().UseNpgsql(connection.ConnectionString).Options);
    }

    [Fact]
    public async Task Existing_users_keep_their_data_and_start_with_a_clean_sign_in_record()
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        await db.GetService<IMigrator>().MigrateAsync("AddScoreFactors");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO users (id, user_name, display_name, role, password_hash, created_at)
            VALUES ('0193f1d2-7c4a-7b1e-9d2a-5a3c1e0b9f11', 'officer', 'Ana Jovanović', 'Officer', 'hash', '2026-10-30 10:00:00+00');
            """);

        await db.Database.MigrateAsync();

        var user = await db.Users.AsNoTracking().SingleAsync();
        Assert.Equal("officer", user.UserName);
        Assert.Equal("Ana Jovanović", user.DisplayName);
        Assert.Equal(UserRole.Officer, user.Role);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(Languages.English, user.PreferredLanguage);
        Assert.Equal(0, user.FailedSignInCount);
        Assert.Null(user.LockedUntil);
        Assert.Null(user.LastSignedInAt);

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task The_database_refuses_an_unknown_language_and_a_negative_failure_count()
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        const string insert = """
            INSERT INTO users (id, user_name, display_name, role, password_hash, created_at, preferred_language, failed_sign_in_count)
            VALUES (gen_random_uuid(), '{0}', 'X', 'Officer', 'hash', now(), '{1}', {2});
            """;
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(string.Format(insert, "a", "de", 0)));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(string.Format(insert, "b", "en", -1)));

        await db.Database.EnsureDeletedAsync();
    }
}
