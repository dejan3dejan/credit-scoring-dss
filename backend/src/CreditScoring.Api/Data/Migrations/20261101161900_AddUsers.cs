using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "decided_by_user_id",
                table: "officer_decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by_user_id",
                table: "credit_applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_officer_decisions_decided_by_user_id",
                table: "officer_decisions",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_submitted_by_user_id",
                table: "credit_applications",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_user_name",
                table: "users",
                column: "user_name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_credit_applications_users_submitted_by_user_id",
                table: "credit_applications",
                column: "submitted_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_officer_decisions_users_decided_by_user_id",
                table: "officer_decisions",
                column: "decided_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_credit_applications_users_submitted_by_user_id",
                table: "credit_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_officer_decisions_users_decided_by_user_id",
                table: "officer_decisions");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_officer_decisions_decided_by_user_id",
                table: "officer_decisions");

            migrationBuilder.DropIndex(
                name: "IX_credit_applications_submitted_by_user_id",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "decided_by_user_id",
                table: "officer_decisions");

            migrationBuilder.DropColumn(
                name: "submitted_by_user_id",
                table: "credit_applications");
        }
    }
}
