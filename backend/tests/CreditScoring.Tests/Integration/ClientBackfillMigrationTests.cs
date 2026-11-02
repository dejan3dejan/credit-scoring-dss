using CreditScoring.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ClientBackfillMigrationTests
{
    private static CreditScoringDbContext Context()
    {
        var connection = new NpgsqlConnectionStringBuilder(DatabaseFixture.ConnectionString) { Database = "credit_scoring_migration_test" };
        return new CreditScoringDbContext(
            new DbContextOptionsBuilder<CreditScoringDbContext>().UseNpgsql(connection.ConnectionString).Options);
    }

    private static string Insert(string name, string submittedAt) => $"""
        INSERT INTO credit_applications (id, applicant_name, submitted_by, submitted_at, duration_months, credit_amount, age,
            installment_share_percent, years_at_residence, credits_at_bank, dependants, checking_account_status, credit_history,
            purpose, savings_account, employment_duration, personal_status_sex, other_debtors, property, other_installment_plans,
            housing, job, has_registered_telephone, is_foreign_worker)
        VALUES (gen_random_uuid(), '{name}', 'dejan', '{submittedAt}', 24, 3500, 35, 22, 2, 1, 0, 'Negative', 'NoCreditsOrAllPaidDuly',
            'FurnitureOrEquipment', 'UnknownOrNone', 'From1To4Years', 'MaleMarriedOrWidowed', 'None', 'RealEstate', 'None',
            'Rent', 'SkilledEmployeeOrOfficial', false, false);
        """;

    [Fact]
    public async Task Existing_applications_get_one_client_per_applicant_name()
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        await db.GetService<IMigrator>().MigrateAsync("AddUsers");
        await db.Database.ExecuteSqlRawAsync(
            Insert("Ana Petrović", "2026-10-30 10:00:00+00")
            + Insert("Ana Petrović", "2026-10-31 10:00:00+00")
            + Insert("Marko Ilić", "2026-10-29 09:00:00+00"));

        await db.GetService<IMigrator>().MigrateAsync("AddScoringFailures");

        var clients = await db.Clients.OrderBy(c => c.FullName).ToListAsync();
        Assert.Equal(["Ana Petrović", "Marko Ilić"], clients.Select(c => c.FullName));
        Assert.All(clients, c => Assert.Matches("^C-[0-9]{6}$", c.ClientNumber));
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 10, 0, 0, TimeSpan.Zero), clients[0].CreatedAt);
        Assert.All(clients, c => Assert.Null(c.CreatedByUserId));

        var linked = await db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM credit_applications a
            JOIN clients c ON c.id = a.client_id AND c.full_name = a.applicant_name
            """).SingleAsync();
        Assert.Equal(3, linked);

        await db.Database.EnsureDeletedAsync();
    }
}
