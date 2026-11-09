using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class BranchesMigrationTests
{
    private static CreditScoringDbContext Context()
    {
        var connection = new NpgsqlConnectionStringBuilder(DatabaseFixture.ConnectionString) { Database = "credit_scoring_branches_migration_test" };
        return new CreditScoringDbContext(
            new DbContextOptionsBuilder<CreditScoringDbContext>().UseNpgsql(connection.ConnectionString).Options);
    }

    private const string ExistingData = """
        INSERT INTO users (id, user_name, display_name, role, password_hash, created_at)
        VALUES ('0192f000-0000-7000-8000-000000000001', 'officer', 'Ana Jovanović', 'Officer', 'hash', now()),
               ('0192f000-0000-7000-8000-000000000002', 'manager', 'Marko Ilić', 'Manager', 'hash', now()),
               ('0192f000-0000-7000-8000-000000000003', 'risk', 'Jelena Petrović', 'Risk', 'hash', now());
        INSERT INTO clients (id, client_number, full_name, date_of_birth, created_at, created_by_user_id)
        VALUES ('0192f000-0000-7000-8000-0000000000c1', 'C-000001', 'Mira Kovač', '1990-05-01', now(), '0192f000-0000-7000-8000-000000000001');
        INSERT INTO credit_applications (id, client_id, applicant_name, submitted_by, submitted_by_user_id, submitted_at, loan_amount, purpose,
            employment_length, home_ownership, annual_income, income_verification, debt_to_income_percent, credit_score, earliest_credit_line,
            inquiries_last_6_months, delinquencies_last_2_years, public_records, bankruptcies, revolving_balance, revolving_utilization_percent,
            open_accounts, total_accounts, mortgage_accounts)
        VALUES ('0192f000-0000-7000-8000-0000000000a1', '0192f000-0000-7000-8000-0000000000c1', 'Mira Kovač', 'Ana Jovanović',
            '0192f000-0000-7000-8000-000000000001', now(), 12000, 'DebtConsolidation', 'FourYears', 'Rent', 58000, 'SourceVerified', 18.4,
            697, '2015-05-01', 1, 0, 0, 0, 9400, 52.3, 9, 21, 0);
        """;

    [Fact]
    public async Task Existing_staff_clients_and_applications_move_to_the_head_office()
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        await db.GetService<IMigrator>().MigrateAsync("AddUserAdministrationAndAudit");
        await db.Database.ExecuteSqlRawAsync(ExistingData);

        await db.Database.MigrateAsync();

        var hq = await db.Branches.SingleAsync();
        Assert.Equal(("HQ", "Head office", null, true), (hq.Code, hq.Name, hq.City, hq.IsActive));
        var users = await db.Users.AsNoTracking().OrderBy(u => u.UserName).ToDictionaryAsync(u => u.UserName);
        Assert.Equal(hq.Id, users["officer"].BranchId);
        Assert.Equal(hq.Id, users["manager"].BranchId);
        Assert.Null(users["risk"].BranchId);
        var client = await db.Clients.AsNoTracking().SingleAsync();
        Assert.Equal(("Mira Kovač", new DateOnly(1990, 5, 1), hq.Id, true, null), (client.FullName, client.DateOfBirth, client.BranchId, client.IsActive, client.AnonymisedAt));
        Assert.Equal((null, null, null, null), (client.Address, client.Phone, client.Email, client.Employer));
        Assert.Equal(hq.Id, (await db.Applications.AsNoTracking().SingleAsync()).BranchId);

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task The_database_requires_a_branch_exactly_for_officers_and_managers()
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        var hq = (await db.Branches.SingleAsync()).Id;

        const string insert = """
            INSERT INTO users (id, user_name, display_name, role, password_hash, created_at, branch_id)
            VALUES (gen_random_uuid(), '{0}', 'X', '{1}', 'hash', now(), {2});
            """;
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(string.Format(insert, "a", "Officer", "NULL")));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(string.Format(insert, "b", "Risk", $"'{hq}'")));
        await db.Database.ExecuteSqlRawAsync(string.Format(insert, "c", "Manager", $"'{hq}'"));
        await db.Database.ExecuteSqlRawAsync(string.Format(insert, "d", "Admin", "NULL"));

        await db.Database.EnsureDeletedAsync();
    }
}
