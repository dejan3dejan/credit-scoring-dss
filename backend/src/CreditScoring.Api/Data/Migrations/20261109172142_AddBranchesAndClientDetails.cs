using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddBranchesAndClientDetails : Migration
    {
        private const string HeadOfficeId = "01a00000-0000-7000-8000-000000000001";
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.id);
                });

            migrationBuilder.Sql($"""
                INSERT INTO branches (id, code, name, city, is_active, created_at)
                VALUES ('{HeadOfficeId}', 'HQ', 'Head office', NULL, true, now());
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "credit_applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql($"""
                UPDATE users SET branch_id = '{HeadOfficeId}' WHERE role IN ('Officer', 'Manager');
                UPDATE clients SET branch_id = '{HeadOfficeId}';
                UPDATE credit_applications a SET branch_id = c.branch_id FROM clients c WHERE c.id = a.client_id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "branch_id",
                table: "clients",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "branch_id",
                table: "credit_applications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "clients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "anonymised_at",
                table: "clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "clients",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "employer",
                table: "clients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "clients",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_branch_id",
                table: "users",
                column: "branch_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_branch_for_branch_roles",
                table: "users",
                sql: "(role IN ('Officer', 'Manager')) = (branch_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_branch_id",
                table: "credit_applications",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_clients_branch_id",
                table: "clients",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_code",
                table: "branches",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_clients_branches_branch_id",
                table: "clients",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_credit_applications_branches_branch_id",
                table: "credit_applications",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_users_branches_branch_id",
                table: "users",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clients_branches_branch_id",
                table: "clients");

            migrationBuilder.DropForeignKey(
                name: "FK_credit_applications_branches_branch_id",
                table: "credit_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_users_branches_branch_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropIndex(
                name: "IX_users_branch_id",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_branch_for_branch_roles",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_credit_applications_branch_id",
                table: "credit_applications");

            migrationBuilder.DropIndex(
                name: "IX_clients_branch_id",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "address",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "anonymised_at",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "email",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "employer",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "clients");
        }
    }
}
