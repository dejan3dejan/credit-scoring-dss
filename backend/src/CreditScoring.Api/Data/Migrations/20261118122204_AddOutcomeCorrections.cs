using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddOutcomeCorrections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "corrected_at",
                table: "loan_outcomes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "corrected_by",
                table: "loan_outcomes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "corrected_by_user_id",
                table: "loan_outcomes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "correction_reason",
                table: "loan_outcomes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_outcomes_corrected_by_user_id",
                table: "loan_outcomes",
                column: "corrected_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_loan_outcomes_users_corrected_by_user_id",
                table: "loan_outcomes",
                column: "corrected_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_loan_outcomes_users_corrected_by_user_id",
                table: "loan_outcomes");

            migrationBuilder.DropIndex(
                name: "IX_loan_outcomes_corrected_by_user_id",
                table: "loan_outcomes");

            migrationBuilder.DropColumn(
                name: "corrected_at",
                table: "loan_outcomes");

            migrationBuilder.DropColumn(
                name: "corrected_by",
                table: "loan_outcomes");

            migrationBuilder.DropColumn(
                name: "corrected_by_user_id",
                table: "loan_outcomes");

            migrationBuilder.DropColumn(
                name: "correction_reason",
                table: "loan_outcomes");
        }
    }
}
