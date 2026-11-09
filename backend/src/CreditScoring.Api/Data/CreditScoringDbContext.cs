using System.Text.RegularExpressions;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Data;

public interface IBranchScope
{
    Guid? BranchId { get; }
}

public class CreditScoringDbContext : DbContext
{
    private readonly IBranchScope? branchScope;

    public CreditScoringDbContext(DbContextOptions<CreditScoringDbContext> options)
        : base(options)
    {
    }

    public CreditScoringDbContext(DbContextOptions<CreditScoringDbContext> options, IBranchScope branchScope)
        : base(options)
    {
        this.branchScope = branchScope;
    }

    private Guid? ScopeBranchId => branchScope?.BranchId;

    public DbSet<CreditApplication> Applications => Set<CreditApplication>();
    public DbSet<CreditDecision> Decisions => Set<CreditDecision>();
    public DbSet<OfficerDecision> OfficerDecisions => Set<OfficerDecision>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ScoringModel> ScoringModels => Set<ScoringModel>();
    public DbSet<ModelEvent> ModelEvents => Set<ModelEvent>();
    public DbSet<ShadowScore> ShadowScores => Set<ShadowScore>();
    public DbSet<ScoringFailure> ScoringFailures => Set<ScoringFailure>();
    public DbSet<ScoreFactor> ScoreFactors => Set<ScoreFactor>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<SignOffRequest> SignOffRequests => Set<SignOffRequest>();
    public DbSet<ApplicationNote> Notes => Set<ApplicationNote>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(64);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("client_number_seq");
        modelBuilder.HasSequence<long>("application_reference_seq");

        modelBuilder.Entity<Client>(client =>
        {
            client.ToTable("clients");
            client.Property(c => c.ClientNumber)
                .HasMaxLength(BusinessLimits.ClientNumberMaxLength)
                .HasDefaultValueSql("'C-' || lpad(nextval('client_number_seq')::text, 6, '0')")
                .HasSentinel("")
                .ValueGeneratedOnAdd();
            client.Property(c => c.FullName).HasMaxLength(BusinessLimits.ApplicantNameMaxLength);
            client.Property(c => c.Address).HasMaxLength(BusinessLimits.AddressMaxLength);
            client.Property(c => c.Phone).HasMaxLength(BusinessLimits.PhoneMaxLength);
            client.Property(c => c.Email).HasMaxLength(BusinessLimits.EmailMaxLength);
            client.Property(c => c.Employer).HasMaxLength(BusinessLimits.EmployerMaxLength);
            client.HasOne<Branch>().WithMany().HasForeignKey(c => c.BranchId).OnDelete(DeleteBehavior.Restrict);
            client.HasQueryFilter(c => ScopeBranchId == null || c.BranchId == ScopeBranchId);
            client.HasIndex(c => c.ClientNumber).IsUnique();
            client.HasIndex(c => c.FullName);
            client.HasOne<User>().WithMany().HasForeignKey(c => c.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditApplication>(application =>
        {
            application.ToTable("credit_applications");
            application.Property(a => a.ApplicantName).HasMaxLength(BusinessLimits.ApplicantNameMaxLength);
            application.Property(a => a.SubmittedBy).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            application.Property(a => a.AnnualIncome).HasPrecision(12, 2);
            application.Property(a => a.DebtToIncomePercent).HasPrecision(5, 2);
            application.Property(a => a.RevolvingBalance).HasPrecision(12, 2);
            application.Property(a => a.RevolvingUtilizationPercent).HasPrecision(5, 2);
            application.HasIndex(a => a.SubmittedAt);
            application.HasIndex(a => a.ApplicantName);
            application.HasOne<User>().WithMany().HasForeignKey(a => a.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
            application.HasOne(a => a.Client).WithMany().HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Restrict);
            application.HasOne<Branch>().WithMany().HasForeignKey(a => a.BranchId).OnDelete(DeleteBehavior.Restrict);
            application.HasQueryFilter(a => ScopeBranchId == null || a.BranchId == ScopeBranchId);
            application.Property(a => a.Reference)
                .HasMaxLength(BusinessLimits.ClientNumberMaxLength)
                .HasDefaultValueSql("'A-' || lpad(nextval('application_reference_seq')::text, 6, '0')")
                .HasSentinel("")
                .ValueGeneratedOnAdd();
            application.HasIndex(a => a.Reference).IsUnique();
            application.HasIndex(a => new { a.Stage, a.AssignedToUserId });
            application.Property(a => a.WithdrawnBy).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            application.Property(a => a.WithdrawalReason).HasMaxLength(BusinessLimits.DecisionReasonMaxLength);
            application.HasOne(a => a.AssignedTo).WithMany().HasForeignKey(a => a.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
            application.HasOne<User>().WithMany().HasForeignKey(a => a.WithdrawnByUserId).OnDelete(DeleteBehavior.Restrict);
            application.HasMany(a => a.SignOffs).WithOne().HasForeignKey(s => s.CreditApplicationId).OnDelete(DeleteBehavior.Cascade);
            application.HasMany<ApplicationNote>().WithOne().HasForeignKey(n => n.CreditApplicationId).OnDelete(DeleteBehavior.Cascade);
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
            {
                table.HasCheckConstraint("ck_scoring_models_threshold_range", "threshold > 0 AND threshold < 1");
                table.HasCheckConstraint("ck_scoring_models_review_from_range", "review_from IS NULL OR (review_from > 0 AND review_from < threshold)");
            });
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
                table.HasCheckConstraint("ck_shadow_scores_review_band", ReviewBandCheck);
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
            decision.Property(d => d.SignedOffBy).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            decision.HasOne<User>().WithMany().HasForeignKey(d => d.SignedOffByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SignOffRequest>(request =>
        {
            request.ToTable("sign_off_requests", table =>
                table.HasCheckConstraint("ck_sign_off_requests_review", "(status = 'Pending') = (reviewed_at IS NULL)"));
            request.Property(r => r.Id).ValueGeneratedNever();
            request.Property(r => r.Reason).HasMaxLength(BusinessLimits.DecisionReasonMaxLength);
            request.Property(r => r.Note).HasMaxLength(BusinessLimits.DecisionReasonMaxLength);
            request.Property(r => r.RequestedBy).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            request.Property(r => r.ReviewedBy).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            request.HasIndex(r => r.CreditApplicationId).IsUnique().HasFilter("status = 'Pending'");
            request.HasOne<User>().WithMany().HasForeignKey(r => r.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            request.HasOne<User>().WithMany().HasForeignKey(r => r.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApplicationNote>(note =>
        {
            note.ToTable("application_notes");
            note.Property(n => n.Id).ValueGeneratedNever();
            note.Property(n => n.Text).HasMaxLength(ApplicationNote.TextMaxLength);
            note.Property(n => n.Author).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            note.HasIndex(n => new { n.CreditApplicationId, n.CreatedAt });
            note.HasOne<User>().WithMany().HasForeignKey(n => n.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
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
                table.HasCheckConstraint("ck_credit_decisions_review_band", ReviewBandCheck);
            });
            decision.Property(d => d.ModelId).HasMaxLength(100);
            decision.Property(d => d.ErrorCode).HasMaxLength(64);
            decision.HasIndex(d => new { d.Status, d.Recommendation });
            decision.HasMany(d => d.Factors)
                .WithOne()
                .HasForeignKey(f => f.CreditDecisionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScoreFactor>(factor =>
        {
            factor.ToTable("score_factors", table =>
                table.HasCheckConstraint("ck_score_factors_rank", "rank >= 1"));
            factor.Property(f => f.Input).HasMaxLength(64);
            factor.HasIndex(f => new { f.CreditDecisionId, f.Rank }).IsUnique();
        });

        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users", table =>
            {
                table.HasCheckConstraint(
                    "ck_users_preferred_language",
                    $"preferred_language IN ({string.Join(", ", Languages.All.Select(l => $"'{l}'"))})");
                table.HasCheckConstraint("ck_users_failed_sign_in_count", "failed_sign_in_count >= 0");
                table.HasCheckConstraint(
                    "ck_users_branch_for_branch_roles",
                    "(role IN ('Officer', 'Manager')) = (branch_id IS NOT NULL)");
            });
            user.HasOne<Branch>().WithMany().HasForeignKey(u => u.BranchId).OnDelete(DeleteBehavior.Restrict);
            user.Property(u => u.UserName).HasMaxLength(BusinessLimits.UserNameMaxLength);
            user.Property(u => u.DisplayName).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            user.Property(u => u.PreferredLanguage).HasMaxLength(8).HasDefaultValue(Languages.English);
            user.Property(u => u.Email).HasMaxLength(BusinessLimits.EmailMaxLength);
            user.Property(u => u.MustChangePassword).HasDefaultValue(false);
            user.Property(u => u.SecurityStamp).HasDefaultValueSql("gen_random_uuid()");
            user.HasIndex(u => u.UserName).IsUnique();
        });

        modelBuilder.Entity<Branch>(branch =>
        {
            branch.ToTable("branches");
            branch.Property(b => b.Id).ValueGeneratedNever();
            branch.Property(b => b.Code).HasMaxLength(Branch.CodeMaxLength);
            branch.Property(b => b.Name).HasMaxLength(Branch.NameMaxLength);
            branch.Property(b => b.City).HasMaxLength(BusinessLimits.CityMaxLength);
            branch.HasIndex(b => b.Code).IsUnique();
        });

        modelBuilder.Entity<AuditEntry>(entry =>
        {
            entry.ToTable("audit_entries");
            entry.Property(e => e.Id).ValueGeneratedNever();
            entry.Property(e => e.UserName).HasMaxLength(BusinessLimits.SubmittedByMaxLength);
            entry.Property(e => e.Entity).HasMaxLength(AuditEntry.EntityMaxLength);
            entry.Property(e => e.EntityId).HasMaxLength(AuditEntry.EntityIdMaxLength);
            entry.Property(e => e.TraceId).HasMaxLength(AuditEntry.TraceIdMaxLength);
            entry.Property(e => e.Changes).HasColumnType("jsonb");
            entry.HasIndex(e => e.At);
            entry.HasIndex(e => new { e.Entity, e.EntityId });
            entry.HasIndex(e => e.UserId);
        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private const string ReviewBandCheck =
        "(review_from_used IS NULL OR (status = 'Scored' AND review_from_used > 0 AND review_from_used < threshold_used))"
        + " AND (recommendation IS DISTINCT FROM 'Review' OR review_from_used IS NOT NULL)";

    private static string ToSnakeCase(string name)
    {
        var split = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1_$2");
        split = Regex.Replace(split, "([a-z0-9])([A-Z])", "$1_$2");
        return Regex.Replace(split, "([a-z])([0-9])", "$1_$2").ToLowerInvariant();
    }
}
