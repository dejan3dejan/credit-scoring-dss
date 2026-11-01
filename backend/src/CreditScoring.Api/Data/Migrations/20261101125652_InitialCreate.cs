using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "credit_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    submitted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_months = table.Column<int>(type: "integer", nullable: false),
                    credit_amount = table.Column<int>(type: "integer", nullable: false),
                    age = table.Column<int>(type: "integer", nullable: false),
                    installment_share_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    years_at_residence = table.Column<int>(type: "integer", nullable: false),
                    credits_at_bank = table.Column<int>(type: "integer", nullable: false),
                    dependants = table.Column<int>(type: "integer", nullable: false),
                    checking_account_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    credit_history = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    savings_account = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    employment_duration = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    personal_status_sex = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    other_debtors = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    property = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    other_installment_plans = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    housing = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    job = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    has_registered_telephone = table.Column<bool>(type: "boolean", nullable: false),
                    is_foreign_worker = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "credit_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    p_bad = table.Column<double>(type: "double precision", nullable: true),
                    model_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    threshold_used = table.Column<double>(type: "double precision", nullable: true),
                    recommendation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_decisions", x => x.id);
                    table.CheckConstraint("ck_credit_decisions_p_bad_range", "p_bad >= 0 AND p_bad <= 1");
                    table.CheckConstraint("ck_credit_decisions_status_fields", "(status = 'Scored' AND p_bad IS NOT NULL AND model_id IS NOT NULL AND threshold_used IS NOT NULL AND recommendation IS NOT NULL AND error_code IS NULL) OR (status = 'ScoringUnavailable' AND error_code IS NOT NULL AND p_bad IS NULL AND model_id IS NULL AND threshold_used IS NULL AND recommendation IS NULL)");
                    table.ForeignKey(
                        name: "FK_credit_decisions_credit_applications_credit_application_id",
                        column: x => x.credit_application_id,
                        principalTable: "credit_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_applicant_name",
                table: "credit_applications",
                column: "applicant_name");

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_submitted_at",
                table: "credit_applications",
                column: "submitted_at");

            migrationBuilder.CreateIndex(
                name: "IX_credit_decisions_credit_application_id",
                table: "credit_decisions",
                column: "credit_application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_decisions_status_recommendation",
                table: "credit_decisions",
                columns: new[] { "status", "recommendation" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credit_decisions");

            migrationBuilder.DropTable(
                name: "credit_applications");
        }
    }
}
