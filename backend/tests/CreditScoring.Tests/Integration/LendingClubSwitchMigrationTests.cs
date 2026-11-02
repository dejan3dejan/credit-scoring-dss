using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class LendingClubSwitchMigrationTests
{
    private static CreditScoringDbContext Context()
    {
        var connection = new NpgsqlConnectionStringBuilder(DatabaseFixture.ConnectionString) { Database = "credit_scoring_switch_test" };
        return new CreditScoringDbContext(
            new DbContextOptionsBuilder<CreditScoringDbContext>().UseNpgsql(connection.ConnectionString).Options);
    }

    private const string GermanData = """
        INSERT INTO clients (id, full_name, created_at) VALUES ('0192f000-0000-7000-8000-0000000000c1', 'Ana Petrović', now());
        INSERT INTO scoring_models (model_id, display_name, dataset, threshold, role, registered_at)
        VALUES ('credit_lr_isotonic_v1', 'LogisticRegression', 'statlog_german_credit', 1.0 / 6.0, 'Shadow', now());
        INSERT INTO credit_applications (id, client_id, applicant_name, submitted_by, submitted_at, duration_months, credit_amount, age,
            installment_share_percent, years_at_residence, credits_at_bank, dependants, checking_account_status, credit_history,
            purpose, savings_account, employment_duration, personal_status_sex, other_debtors, property, other_installment_plans,
            housing, job, has_registered_telephone, is_foreign_worker)
        VALUES ('0192f000-0000-7000-8000-0000000000a1', '0192f000-0000-7000-8000-0000000000c1', 'Ana Petrović', 'dejan', now(),
            24, 3500, 35, 22, 2, 1, 0, 'Negative', 'NoCreditsOrAllPaidDuly', 'FurnitureOrEquipment', 'UnknownOrNone', 'From1To4Years',
            'MaleMarriedOrWidowed', 'None', 'RealEstate', 'None', 'Rent', 'SkilledEmployeeOrOfficial', false, false);
        INSERT INTO credit_decisions (id, credit_application_id, status, evaluated_at, p_bad, model_id, threshold_used, recommendation)
        VALUES (gen_random_uuid(), '0192f000-0000-7000-8000-0000000000a1', 'Scored', now(), 0.1, 'credit_rf_isotonic_v1', 1.0 / 6.0, 'Approve');
        INSERT INTO shadow_scores (id, credit_application_id, model_id, status, p_bad, threshold_used, recommendation, scored_at)
        VALUES (gen_random_uuid(), '0192f000-0000-7000-8000-0000000000a1', 'credit_lr_isotonic_v1', 'Scored', 0.2, 1.0 / 6.0, 'Reject', now());
        """;

    [Fact]
    public async Task Switch_resets_german_applications_keeps_clients_and_makes_the_lending_club_model_active()
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        await db.GetService<IMigrator>().MigrateAsync("AddScoringFailures");
        await db.Database.ExecuteSqlRawAsync(GermanData);

        await db.Database.MigrateAsync();

        Assert.False(await db.Applications.AnyAsync());
        Assert.False(await db.Decisions.AnyAsync());
        Assert.False(await db.ShadowScores.AnyAsync());
        Assert.Equal(["Ana Petrović"], await db.Clients.Select(c => c.FullName).ToListAsync());

        var models = await db.ScoringModels.OrderBy(m => m.ModelId).ToListAsync();
        Assert.Equal(
            [
                ("credit_lc_xgb_isotonic_v1", ModelRole.Active, "lending_club"),
                ("credit_lr_isotonic_v1", ModelRole.Retired, "statlog_german_credit"),
                ("credit_rf_isotonic_v1", ModelRole.Retired, "statlog_german_credit"),
            ],
            models.Select(m => (m.ModelId, m.Role, m.Dataset)));
        var active = models[0];
        Assert.Equal((TestData.Threshold, TestData.ReviewFrom), (active.Threshold, active.ReviewFrom));
        Assert.All(models.Skip(1), m => Assert.Null(m.ReviewFrom));

        var retirements = await db.ModelEvents.Where(e => e.ToRole == ModelRole.Retired).OrderBy(e => e.ModelId).ToListAsync();
        Assert.Equal(
            [("credit_lr_isotonic_v1", ModelRole.Shadow), ("credit_rf_isotonic_v1", ModelRole.Active)],
            retirements.Select(e => (e.ModelId, e.FromRole!.Value)));
        Assert.All(retirements, e => Assert.Equal("System", e.ChangedBy));
        Assert.All(retirements, e => Assert.Contains("Lending Club", e.Reason));
        Assert.True(await db.ModelEvents.AnyAsync(e => e.ModelId == "credit_lc_xgb_isotonic_v1" && e.ToRole == ModelRole.Active));

        await db.Database.EnsureDeletedAsync();
    }
}
