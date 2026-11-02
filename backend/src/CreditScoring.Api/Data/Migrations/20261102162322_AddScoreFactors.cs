using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddScoreFactors : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "score_factors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    input = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    contribution = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_factors", x => x.id);
                    table.CheckConstraint("ck_score_factors_rank", "rank >= 1");
                    table.ForeignKey(
                        name: "FK_score_factors_credit_decisions_credit_decision_id",
                        column: x => x.credit_decision_id,
                        principalTable: "credit_decisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_score_factors_credit_decision_id_rank",
                table: "score_factors",
                columns: new[] { "credit_decision_id", "rank" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "score_factors");
        }
    }
}
