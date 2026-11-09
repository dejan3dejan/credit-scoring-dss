using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddApplicationLifecycle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "application_reference_seq");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "signed_off_at",
                table: "officer_decisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signed_off_by",
                table: "officer_decisions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "signed_off_by_user_id",
                table: "officer_decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_to_user_id",
                table: "credit_applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference",
                table: "credit_applications",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stage",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "withdrawal_reason",
                table: "credit_applications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "withdrawn_at",
                table: "credit_applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "withdrawn_by",
                table: "credit_applications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "withdrawn_by_user_id",
                table: "credit_applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                WITH ordered AS (SELECT id, row_number() OVER (ORDER BY submitted_at, id) AS n FROM credit_applications)
                UPDATE credit_applications a SET reference = 'A-' || lpad(o.n::text, 6, '0') FROM ordered o WHERE o.id = a.id;
                SELECT setval('application_reference_seq', (SELECT count(*) FROM credit_applications) + 1, false);
                UPDATE credit_applications a SET stage = CASE
                    WHEN EXISTS (SELECT 1 FROM officer_decisions d WHERE d.credit_application_id = a.id) THEN 'Decided'
                    ELSE 'Submitted' END;
                UPDATE credit_applications SET assigned_to_user_id = submitted_by_user_id;
                ALTER TABLE credit_applications
                    ALTER COLUMN reference SET NOT NULL,
                    ALTER COLUMN reference SET DEFAULT 'A-' || lpad(nextval('application_reference_seq')::text, 6, '0'),
                    ALTER COLUMN stage DROP DEFAULT;
                """);

            migrationBuilder.CreateTable(
                name: "application_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_application_notes_credit_applications_credit_application_id",
                        column: x => x.credit_application_id,
                        principalTable: "credit_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_application_notes_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sign_off_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    why = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requested_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reviewed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sign_off_requests", x => x.id);
                    table.CheckConstraint("ck_sign_off_requests_review", "(status = 'Pending') = (reviewed_at IS NULL)");
                    table.ForeignKey(
                        name: "FK_sign_off_requests_credit_applications_credit_application_id",
                        column: x => x.credit_application_id,
                        principalTable: "credit_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sign_off_requests_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sign_off_requests_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_officer_decisions_signed_off_by_user_id",
                table: "officer_decisions",
                column: "signed_off_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_assigned_to_user_id",
                table: "credit_applications",
                column: "assigned_to_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_reference",
                table: "credit_applications",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_stage_assigned_to_user_id",
                table: "credit_applications",
                columns: new[] { "stage", "assigned_to_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_withdrawn_by_user_id",
                table: "credit_applications",
                column: "withdrawn_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_notes_author_user_id",
                table: "application_notes",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_notes_credit_application_id_created_at",
                table: "application_notes",
                columns: new[] { "credit_application_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sign_off_requests_credit_application_id",
                table: "sign_off_requests",
                column: "credit_application_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_sign_off_requests_requested_by_user_id",
                table: "sign_off_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sign_off_requests_reviewed_by_user_id",
                table: "sign_off_requests",
                column: "reviewed_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_credit_applications_users_assigned_to_user_id",
                table: "credit_applications",
                column: "assigned_to_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_credit_applications_users_withdrawn_by_user_id",
                table: "credit_applications",
                column: "withdrawn_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_officer_decisions_users_signed_off_by_user_id",
                table: "officer_decisions",
                column: "signed_off_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_credit_applications_users_assigned_to_user_id",
                table: "credit_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_credit_applications_users_withdrawn_by_user_id",
                table: "credit_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_officer_decisions_users_signed_off_by_user_id",
                table: "officer_decisions");

            migrationBuilder.DropTable(
                name: "application_notes");

            migrationBuilder.DropTable(
                name: "sign_off_requests");

            migrationBuilder.DropIndex(
                name: "IX_officer_decisions_signed_off_by_user_id",
                table: "officer_decisions");

            migrationBuilder.DropIndex(
                name: "IX_credit_applications_assigned_to_user_id",
                table: "credit_applications");

            migrationBuilder.DropIndex(
                name: "IX_credit_applications_reference",
                table: "credit_applications");

            migrationBuilder.DropIndex(
                name: "IX_credit_applications_stage_assigned_to_user_id",
                table: "credit_applications");

            migrationBuilder.DropIndex(
                name: "IX_credit_applications_withdrawn_by_user_id",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "signed_off_at",
                table: "officer_decisions");

            migrationBuilder.DropColumn(
                name: "signed_off_by",
                table: "officer_decisions");

            migrationBuilder.DropColumn(
                name: "signed_off_by_user_id",
                table: "officer_decisions");

            migrationBuilder.DropColumn(
                name: "assigned_to_user_id",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "reference",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "stage",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "withdrawal_reason",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "withdrawn_at",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "withdrawn_by",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "withdrawn_by_user_id",
                table: "credit_applications");

            migrationBuilder.DropSequence(
                name: "application_reference_seq");
        }
    }
}
