using CreditScoring.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class OneOpenApplicationMigrationTests
{
    private static CreditScoringDbContext Context()
    {
        var connection = new NpgsqlConnectionStringBuilder(DatabaseFixture.ConnectionString) { Database = "credit_scoring_one_open_migration_test" };
        return new CreditScoringDbContext(
            new DbContextOptionsBuilder<CreditScoringDbContext>().UseNpgsql(connection.ConnectionString).Options);
    }

    private static string Application(string id, string reference, string stage) => $"""
        INSERT INTO credit_applications (id, client_id, branch_id, reference, stage, applicant_name, submitted_by, submitted_at, loan_amount,
            purpose, employment_length, home_ownership, annual_income, income_verification, debt_to_income_percent, credit_score,
            earliest_credit_line, inquiries_last_6_months, delinquencies_last_2_years, public_records, bankruptcies, revolving_balance,
            revolving_utilization_percent, open_accounts, total_accounts, mortgage_accounts)
        VALUES ('{id}', '0192f000-0000-7000-8000-0000000000c1', '01a00000-0000-7000-8000-000000000001', '{reference}', '{stage}',
            'Mira Kovač', 'Ana Jovanović', now(), 12000, 'DebtConsolidation', 'FourYears', 'Rent', 58000, 'SourceVerified', 18.4, 697,
            '2015-05-01', 1, 0, 0, 0, 9400, 52.3, 9, 21, 0);
        """;

    private const string Client = """
        INSERT INTO clients (id, client_number, full_name, created_at, branch_id)
        VALUES ('0192f000-0000-7000-8000-0000000000c1', 'C-000001', 'Mira Kovač', now(), '01a00000-0000-7000-8000-000000000001');
        """;

    private static async Task<CreditScoringDbContext> AtPreviousMigration(string data)
    {
        var db = Context();
        await db.Database.EnsureDeletedAsync();
        await db.GetService<IMigrator>().MigrateAsync("AddLoanOutcomes");
        await db.Database.ExecuteSqlRawAsync(Client + data);
        return db;
    }

    [Fact]
    public async Task Two_open_applications_for_one_client_stop_the_migration_and_nothing_changes()
    {
        await using var db = await AtPreviousMigration(
            Application("0192f000-0000-7000-8000-0000000000a1", "A-000001", "Submitted")
            + Application("0192f000-0000-7000-8000-0000000000a2", "A-000002", "AwaitingSignOff"));

        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());

        Assert.Contains("A-000001", ex.MessageText);
        Assert.Contains("A-000002", ex.MessageText);
        Assert.Contains("AddLoanOutcomes", (await db.Database.GetAppliedMigrationsAsync()).Last());
        var open = await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM credit_applications WHERE stage = 'Submitted' OR stage = 'AwaitingSignOff'").SingleAsync();
        Assert.Equal(2, open);

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task One_open_application_beside_closed_ones_migrates()
    {
        await using var db = await AtPreviousMigration(
            Application("0192f000-0000-7000-8000-0000000000a1", "A-000001", "Submitted")
            + Application("0192f000-0000-7000-8000-0000000000a2", "A-000002", "Decided")
            + Application("0192f000-0000-7000-8000-0000000000a3", "A-000003", "Withdrawn"));

        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(3, await db.Applications.CountAsync());

        await db.Database.EnsureDeletedAsync();
    }
}
