using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class LifecycleMigrationTests
{
    private static CreditScoringDbContext Context()
    {
        var connection = new NpgsqlConnectionStringBuilder(DatabaseFixture.ConnectionString) { Database = "credit_scoring_lifecycle_migration_test" };
        return new CreditScoringDbContext(
            new DbContextOptionsBuilder<CreditScoringDbContext>().UseNpgsql(connection.ConnectionString).Options);
    }

    private static string Application(string id, string submittedAt, string? submittedBy) => $"""
        INSERT INTO credit_applications (id, client_id, branch_id, applicant_name, submitted_by, submitted_by_user_id, submitted_at, loan_amount,
            purpose, employment_length, home_ownership, annual_income, income_verification, debt_to_income_percent, credit_score,
            earliest_credit_line, inquiries_last_6_months, delinquencies_last_2_years, public_records, bankruptcies, revolving_balance,
            revolving_utilization_percent, open_accounts, total_accounts, mortgage_accounts)
        VALUES ('{id}', '0192f000-0000-7000-8000-0000000000c1', '01a00000-0000-7000-8000-000000000001', 'Mira Kovač', 'Ana Jovanović',
            {(submittedBy is null ? "NULL" : $"'{submittedBy}'")}, '{submittedAt}', 12000, 'DebtConsolidation', 'FourYears', 'Rent', 58000,
            'SourceVerified', 18.4, 697, '2015-05-01', 1, 0, 0, 0, 9400, 52.3, 9, 21, 0);
        """;

    [Fact]
    public async Task Existing_applications_get_a_stage_a_reference_in_submission_order_and_their_submitter_as_owner()
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        await db.GetService<IMigrator>().MigrateAsync("AddBranchesAndClientDetails");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO users (id, user_name, display_name, role, password_hash, created_at, branch_id)
            VALUES ('0192f000-0000-7000-8000-000000000001', 'officer', 'Ana Jovanović', 'Officer', 'hash', now(), '01a00000-0000-7000-8000-000000000001');
            INSERT INTO clients (id, client_number, full_name, created_at, branch_id)
            VALUES ('0192f000-0000-7000-8000-0000000000c1', 'C-000001', 'Mira Kovač', now(), '01a00000-0000-7000-8000-000000000001');
            """
            + Application("0192f000-0000-7000-8000-0000000000a2", "2026-10-31 10:00:00+00", "0192f000-0000-7000-8000-000000000001")
            + Application("0192f000-0000-7000-8000-0000000000a1", "2026-10-30 10:00:00+00", null)
            + """
            INSERT INTO officer_decisions (id, credit_application_id, outcome, decided_by, decided_by_user_id, decided_at, is_override)
            VALUES (gen_random_uuid(), '0192f000-0000-7000-8000-0000000000a2', 'Approve', 'Ana Jovanović', '0192f000-0000-7000-8000-000000000001', now(), false);
            """);

        await db.Database.MigrateAsync();

        var applications = await db.Applications.AsNoTracking().OrderBy(a => a.SubmittedAt).ToListAsync();
        Assert.Equal(["A-000001", "A-000002"], applications.Select(a => a.Reference));
        Assert.Equal([ApplicationStage.Submitted, ApplicationStage.Decided], applications.Select(a => a.Stage));
        Assert.Equal([null, Guid.Parse("0192f000-0000-7000-8000-000000000001")], applications.Select(a => a.AssignedToUserId));
        Assert.Null((await db.OfficerDecisions.AsNoTracking().SingleAsync()).SignedOffBy);
        Assert.False(await db.LoanOutcomes.AnyAsync());
        var next = await db.Database.SqlQueryRaw<string>("SELECT 'A-' || lpad(nextval('application_reference_seq')::text, 6, '0') AS \"Value\"").SingleAsync();
        Assert.Equal("A-000003", next);

        await db.Database.EnsureDeletedAsync();
    }
}
