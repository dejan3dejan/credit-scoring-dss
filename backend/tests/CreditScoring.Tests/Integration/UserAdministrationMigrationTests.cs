using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class UserAdministrationMigrationTests
{
    private static CreditScoringDbContext Context()
    {
        var connection = new NpgsqlConnectionStringBuilder(DatabaseFixture.ConnectionString) { Database = "credit_scoring_user_admin_migration_test" };
        return new CreditScoringDbContext(
            new DbContextOptionsBuilder<CreditScoringDbContext>().UseNpgsql(connection.ConnectionString).Options);
    }

    [Fact]
    public async Task Existing_users_stay_active_with_their_own_stamp_and_no_forced_password_change()
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        await db.GetService<IMigrator>().MigrateAsync("AddUserSignInState");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO users (id, user_name, display_name, role, password_hash, created_at, preferred_language, failed_sign_in_count)
            VALUES ('0193f1d2-7c4a-7b1e-9d2a-5a3c1e0b9f11', 'officer', 'Ana Jovanović', 'Officer', 'hash', '2026-10-30 10:00:00+00', 'sr', 2),
                   ('0193f1d2-7c4a-7b1e-9d2a-5a3c1e0b9f12', 'risk', 'Jelena Petrović', 'Risk', 'hash2', '2026-10-30 10:00:00+00', 'en', 0);
            """);

        await db.Database.MigrateAsync();

        var users = await db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        var officer = users[0];
        Assert.Equal(("officer", "Ana Jovanović", UserRole.Officer, "hash", "sr", 2),
            (officer.UserName, officer.DisplayName, officer.Role, officer.PasswordHash, officer.PreferredLanguage, officer.FailedSignInCount));
        Assert.All(users, u =>
        {
            Assert.True(u.IsActive);
            Assert.False(u.MustChangePassword);
            Assert.Null(u.Email);
            Assert.NotEqual(Guid.Empty, u.SecurityStamp);
        });
        Assert.NotEqual(users[0].SecurityStamp, users[1].SecurityStamp);
        Assert.Empty(await db.AuditEntries.ToListAsync());

        await db.Database.EnsureDeletedAsync();
    }
}
