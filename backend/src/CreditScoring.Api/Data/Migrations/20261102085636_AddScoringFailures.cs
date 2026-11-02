using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddScoringFailures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scoring_failures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_failures", x => x.id);
                    table.ForeignKey(
                        name: "FK_scoring_failures_credit_applications_credit_application_id",
                        column: x => x.credit_application_id,
                        principalTable: "credit_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scoring_failures_credit_application_id_attempted_at",
                table: "scoring_failures",
                columns: new[] { "credit_application_id", "attempted_at" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scoring_failures");
        }
    }
}
