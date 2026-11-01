using System;
using CreditScoring.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    [DbContext(typeof(CreditScoringDbContext))]
    partial class CreditScoringDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.HasSequence("client_number_seq");

            modelBuilder.Entity("CreditScoring.Api.Domain.Client", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("ClientNumber")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)")
                        .HasColumnName("client_number")
                        .HasDefaultValueSql("'C-' || lpad(nextval('client_number_seq')::text, 6, '0')");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<Guid?>("CreatedByUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("created_by_user_id");

                    b.Property<DateOnly?>("DateOfBirth")
                        .HasColumnType("date")
                        .HasColumnName("date_of_birth");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("full_name");

                    b.HasKey("Id");

                    b.HasIndex("ClientNumber")
                        .IsUnique();

                    b.HasIndex("CreatedByUserId");

                    b.HasIndex("FullName");

                    b.ToTable("clients", (string)null);
                });

            modelBuilder.Entity("CreditScoring.Api.Domain.CreditApplication", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int>("Age")
                        .HasColumnType("integer")
                        .HasColumnName("age");

                    b.Property<string>("ApplicantName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("applicant_name");

                    b.Property<string>("CheckingAccountStatus")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("checking_account_status");

                    b.Property<Guid>("ClientId")
                        .HasColumnType("uuid")
                        .HasColumnName("client_id");

                    b.Property<int>("CreditAmount")
                        .HasColumnType("integer")
                        .HasColumnName("credit_amount");

                    b.Property<string>("CreditHistory")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("credit_history");

                    b.Property<int>("CreditsAtBank")
                        .HasColumnType("integer")
                        .HasColumnName("credits_at_bank");

                    b.Property<int>("Dependants")
                        .HasColumnType("integer")
                        .HasColumnName("dependants");

                    b.Property<int>("DurationMonths")
                        .HasColumnType("integer")
                        .HasColumnName("duration_months");

                    b.Property<string>("EmploymentDuration")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("employment_duration");

                    b.Property<bool>("HasRegisteredTelephone")
                        .HasColumnType("boolean")
                        .HasColumnName("has_registered_telephone");

                    b.Property<string>("Housing")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("housing");

                    b.Property<decimal>("InstallmentSharePercent")
                        .HasPrecision(5, 2)
                        .HasColumnType("numeric(5,2)")
                        .HasColumnName("installment_share_percent");

                    b.Property<bool>("IsForeignWorker")
                        .HasColumnType("boolean")
                        .HasColumnName("is_foreign_worker");

                    b.Property<string>("Job")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("job");

                    b.Property<string>("OtherDebtors")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("other_debtors");

                    b.Property<string>("OtherInstallmentPlans")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("other_installment_plans");

                    b.Property<string>("PersonalStatusSex")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("personal_status_sex");

                    b.Property<string>("Property")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("property");

                    b.Property<string>("Purpose")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("purpose");

                    b.Property<string>("SavingsAccount")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("savings_account");

                    b.Property<DateTimeOffset>("SubmittedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("submitted_at");

                    b.Property<string>("SubmittedBy")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("submitted_by");

                    b.Property<Guid?>("SubmittedByUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("submitted_by_user_id");

                    b.Property<int>("YearsAtResidence")
                        .HasColumnType("integer")
                        .HasColumnName("years_at_residence");

                    b.HasKey("Id");

                    b.HasIndex("ApplicantName");

                    b.HasIndex("ClientId");

                    b.HasIndex("SubmittedAt");

                    b.HasIndex("SubmittedByUserId");

                    b.ToTable("credit_applications", (string)null);
                });

            modelBuilder.Entity("CreditScoring.Api.Domain.CreditDecision", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("CreditApplicationId")
                        .HasColumnType("uuid")
                        .HasColumnName("credit_application_id");

                    b.Property<string>("ErrorCode")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("error_code");

                    b.Property<DateTimeOffset>("EvaluatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("evaluated_at");

                    b.Property<string>("ModelId")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("model_id");

                    b.Property<double?>("PBad")
                        .HasColumnType("double precision")
                        .HasColumnName("p_bad");

                    b.Property<string>("Recommendation")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("recommendation");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("status");

                    b.Property<double?>("ThresholdUsed")
                        .HasColumnType("double precision")
                        .HasColumnName("threshold_used");

                    b.HasKey("Id");

                    b.HasIndex("CreditApplicationId")
                        .IsUnique();

                    b.HasIndex("Status", "Recommendation");

                    b.ToTable("credit_decisions", null, t =>
                        {
                            t.HasCheckConstraint("ck_credit_decisions_p_bad_range", "p_bad >= 0 AND p_bad <= 1");

                            t.HasCheckConstraint("ck_credit_decisions_status_fields", "(status = 'Scored' AND p_bad IS NOT NULL AND model_id IS NOT NULL AND threshold_used IS NOT NULL AND recommendation IS NOT NULL AND error_code IS NULL) OR (status = 'ScoringUnavailable' AND error_code IS NOT NULL AND p_bad IS NULL AND model_id IS NULL AND threshold_used IS NULL AND recommendation IS NULL)");
                        });
                });

            modelBuilder.Entity("CreditScoring.Api.Domain.OfficerDecision", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("CreditApplicationId")
                        .HasColumnType("uuid")
                        .HasColumnName("credit_application_id");

                    b.Property<DateTimeOffset>("DecidedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("decided_at");

                    b.Property<string>("DecidedBy")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("decided_by");

                    b.Property<Guid?>("DecidedByUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("decided_by_user_id");

                    b.Property<bool>("IsOverride")
                        .HasColumnType("boolean")
                        .HasColumnName("is_override");

                    b.Property<string>("Outcome")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("outcome");

                    b.Property<string>("Reason")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("reason");

                    b.HasKey("Id");

                    b.HasIndex("CreditApplicationId")
                        .IsUnique();

                    b.HasIndex("DecidedByUserId");

                    b.HasIndex("IsOverride", "Outcome");

                    b.ToTable("officer_decisions", null, t =>
                        {
                            t.HasCheckConstraint("ck_officer_decisions_override_reason", "is_override = false OR reason IS NOT NULL");
                        });
                });

            modelBuilder.Entity("CreditScoring.Api.Domain.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("display_name");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("password_hash");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("role");

                    b.Property<string>("UserName")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("user_name");

                    b.HasKey("Id");

                    b.HasIndex("UserName")
                        .IsUnique();

                    b.ToTable("users", (string)null);
                });

            modelBuilder.Entity("CreditScoring.Api.Domain.Client", b =>
                {
                    b.HasOne("CreditScoring.Api.Domain.User", null)
                        .WithMany()
                        .HasForeignKey("CreatedByUserId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("CreditScoring.Api.Domain.CreditApplication", b =>
                {
                    b.HasOne("CreditScoring.Api.Domain.Client", "Client")
                        .WithMany()
                        .HasForeignKey("ClientId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("CreditScoring.Api.Domain.User", null)
                        .WithMany()
                        .HasForeignKey("SubmittedByUserId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("Client");
                });

            modelBuilder.Entity("CreditScoring.Api.Domain.CreditDecision", b =>
                {
                    b.HasOne("CreditScoring.Api.Domain.CreditApplication", null)
                        .WithOne("Decision")
                        .HasForeignKey("CreditScoring.Api.Domain.CreditDecision", "CreditApplicationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("CreditScoring.Api.Domain.OfficerDecision", b =>
                {
                    b.HasOne("CreditScoring.Api.Domain.CreditApplication", null)
                        .WithOne("OfficerDecision")
                        .HasForeignKey("CreditScoring.Api.Domain.OfficerDecision", "CreditApplicationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("CreditScoring.Api.Domain.User", null)
                        .WithMany()
                        .HasForeignKey("DecidedByUserId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("CreditScoring.Api.Domain.CreditApplication", b =>
                {
                    b.Navigation("Decision");

                    b.Navigation("OfficerDecision");
                });
#pragma warning restore 612, 618
        }
    }
}
