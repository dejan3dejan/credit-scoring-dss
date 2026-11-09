using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddUserSignInState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_sign_in_count",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_signed_in_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_language",
                table: "users",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_failed_sign_in_count",
                table: "users",
                sql: "failed_sign_in_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_preferred_language",
                table: "users",
                sql: "preferred_language IN ('en', 'sr')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_failed_sign_in_count",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_preferred_language",
                table: "users");

            migrationBuilder.DropColumn(
                name: "failed_sign_in_count",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_signed_in_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "locked_until",
                table: "users");

            migrationBuilder.DropColumn(
                name: "preferred_language",
                table: "users");
        }
    }
}
