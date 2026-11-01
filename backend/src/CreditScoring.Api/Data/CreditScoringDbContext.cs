using System.Text.RegularExpressions;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Data;

public class CreditScoringDbContext(DbContextOptions<CreditScoringDbContext> options) : DbContext(options)
{
    public DbSet<CreditApplication> Applications => Set<CreditApplication>();
    public DbSet<CreditDecision> Decisions => Set<CreditDecision>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(64);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CreditApplication>(application =>
        {
            application.ToTable("credit_applications");
            application.Property(a => a.ApplicantName).HasMaxLength(200);
            application.Property(a => a.SubmittedBy).HasMaxLength(100);
            application.Property(a => a.InstallmentSharePercent).HasPrecision(5, 2);
            application.HasIndex(a => a.SubmittedAt);
            application.HasIndex(a => a.ApplicantName);
            application.HasOne(a => a.Decision)
                .WithOne()
                .HasForeignKey<CreditDecision>(d => d.CreditApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CreditDecision>(decision =>
        {
            decision.ToTable("credit_decisions", table =>
            {
                table.HasCheckConstraint(
                    "ck_credit_decisions_status_fields",
                    "(status = 'Scored' AND p_bad IS NOT NULL AND model_id IS NOT NULL AND threshold_used IS NOT NULL AND recommendation IS NOT NULL AND error_code IS NULL)"
                    + " OR (status = 'ScoringUnavailable' AND error_code IS NOT NULL AND p_bad IS NULL AND model_id IS NULL AND threshold_used IS NULL AND recommendation IS NULL)");
                table.HasCheckConstraint("ck_credit_decisions_p_bad_range", "p_bad >= 0 AND p_bad <= 1");
            });
            decision.Property(d => d.ModelId).HasMaxLength(100);
            decision.Property(d => d.ErrorCode).HasMaxLength(64);
            decision.HasIndex(d => new { d.Status, d.Recommendation });
        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        var split = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1_$2");
        return Regex.Replace(split, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
    }
}
