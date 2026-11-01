using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddOfficerDecisions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "officer_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    decided_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_override = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_officer_decisions", x => x.id);
                    table.CheckConstraint("ck_officer_decisions_override_reason", "is_override = false OR reason IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_officer_decisions_credit_applications_credit_application_id",
                        column: x => x.credit_application_id,
                        principalTable: "credit_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_officer_decisions_credit_application_id",
                table: "officer_decisions",
                column: "credit_application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_officer_decisions_is_override_outcome",
                table: "officer_decisions",
                columns: new[] { "is_override", "outcome" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "officer_decisions");
        }
    }
}
