using System.Text.RegularExpressions;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Data;

public class CreditScoringDbContext(DbContextOptions<CreditScoringDbContext> options) : DbContext(options)
{
    public DbSet<CreditApplication> Applications => Set<CreditApplication>();
    public DbSet<CreditDecision> Decisions => Set<CreditDecision>();
    public DbSet<OfficerDecision> OfficerDecisions => Set<OfficerDecision>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ScoringModel> ScoringModels => Set<ScoringModel>();
    public DbSet<ModelEvent> ModelEvents => Set<ModelEvent>();
    public DbSet<ShadowScore> ShadowScores => Set<ShadowScore>();
    public DbSet<ScoringFailure> ScoringFailures => Set<ScoringFailure>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(64);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("client_number_seq");

        modelBuilder.Entity<Client>(client =>
        {
            client.ToTable("clients");
            client.Property(c => c.ClientNumber)
                .HasMaxLength(BusinessLimits.ClientNumberMaxLength)
                .HasDefaultValueSql("'C-' || lpad(nextval('client_number_seq')::text, 6, '0')")
                .HasSentinel("")
                .ValueGeneratedOnAdd();
            client.Property(c => c.FullName).HasMaxLength(BusinessLimits.ApplicantNameMaxLength);
            client.HasIndex(c => c.ClientNumber).IsUnique();
            client.HasIndex(c => c.FullName);
            client.HasOne<User>().WithMany().HasForeignKey(c => c.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditApplication>(application =>
        {
            application.ToTable("credit_applications");
            application.Property(a => a.ApplicantName).HasMaxLength(BusinessLimits.ApplicantNameMaxLength);
            application.Property(a => a.SubmittedBy).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            application.Property(a => a.InstallmentSharePercent).HasPrecision(5, 2);
            application.HasIndex(a => a.SubmittedAt);
            application.HasIndex(a => a.ApplicantName);
            application.HasOne<User>().WithMany().HasForeignKey(a => a.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
            application.HasOne(a => a.Client).WithMany().HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Restrict);
            application.HasOne(a => a.Decision)
                .WithOne()
                .HasForeignKey<CreditDecision>(d => d.CreditApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            application.HasOne(a => a.OfficerDecision)
                .WithOne()
                .HasForeignKey<OfficerDecision>(d => d.CreditApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            application.HasMany(a => a.ShadowScores)
                .WithOne()
                .HasForeignKey(s => s.CreditApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            application.HasMany(a => a.ScoringFailures)
                .WithOne()
                .HasForeignKey(f => f.CreditApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScoringFailure>(failure =>
        {
            failure.ToTable("scoring_failures");
            failure.Property(f => f.ErrorCode).HasMaxLength(64);
            failure.HasIndex(f => new { f.CreditApplicationId, f.AttemptedAt });
        });

        modelBuilder.Entity<ScoringModel>(model =>
        {
            model.ToTable("scoring_models", table =>
                table.HasCheckConstraint("ck_scoring_models_threshold_range", "threshold > 0 AND threshold < 1"));
            model.HasKey(m => m.ModelId);
            model.Property(m => m.ModelId).HasMaxLength(BusinessLimits.ModelIdMaxLength);
            model.Property(m => m.DisplayName).HasMaxLength(BusinessLimits.ModelIdMaxLength);
            model.Property(m => m.Dataset).HasMaxLength(BusinessLimits.ModelIdMaxLength);
            model.HasOne<User>().WithMany().HasForeignKey(m => m.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ModelEvent>(change =>
        {
            change.ToTable("model_events");
            change.Property(e => e.ModelId).HasMaxLength(BusinessLimits.ModelIdMaxLength);
            change.Property(e => e.Reason).HasMaxLength(BusinessLimits.DecisionReasonMaxLength);
            change.Property(e => e.ChangedBy).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            change.HasIndex(e => e.ChangedAt);
            change.HasOne<ScoringModel>().WithMany().HasForeignKey(e => e.ModelId).OnDelete(DeleteBehavior.Restrict);
            change.HasOne<User>().WithMany().HasForeignKey(e => e.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShadowScore>(shadow =>
        {
            shadow.ToTable("shadow_scores", table =>
            {
                table.HasCheckConstraint(
                    "ck_shadow_scores_status_fields",
                    "(status = 'Scored' AND p_bad IS NOT NULL AND threshold_used IS NOT NULL AND recommendation IS NOT NULL AND error_code IS NULL)"
                    + " OR (status = 'ScoringUnavailable' AND error_code IS NOT NULL AND p_bad IS NULL AND threshold_used IS NULL AND recommendation IS NULL)");
                table.HasCheckConstraint("ck_shadow_scores_p_bad_range", "p_bad >= 0 AND p_bad <= 1");
            });
            shadow.Property(s => s.ModelId).HasMaxLength(BusinessLimits.ModelIdMaxLength);
            shadow.Property(s => s.ErrorCode).HasMaxLength(64);
            shadow.HasIndex(s => new { s.CreditApplicationId, s.ModelId }).IsUnique();
            shadow.HasOne<ScoringModel>().WithMany().HasForeignKey(s => s.ModelId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OfficerDecision>(decision =>
        {
            decision.ToTable("officer_decisions", table =>
                table.HasCheckConstraint("ck_officer_decisions_override_reason", "is_override = false OR reason IS NOT NULL"));
            decision.Property(d => d.Reason).HasMaxLength(BusinessLimits.DecisionReasonMaxLength);
            decision.Property(d => d.DecidedBy).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            decision.HasIndex(d => new { d.IsOverride, d.Outcome });
            decision.HasOne<User>().WithMany().HasForeignKey(d => d.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.UserName).HasMaxLength(BusinessLimits.UserNameMaxLength);
            user.Property(u => u.DisplayName).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            user.HasIndex(u => u.UserName).IsUnique();
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
