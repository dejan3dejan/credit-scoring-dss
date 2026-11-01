using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddClients : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "client_number_seq");

            migrationBuilder.AddColumn<Guid>(
                name: "client_id",
                table: "credit_applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValueSql: "'C-' || lpad(nextval('client_number_seq')::text, 6, '0')"),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                    table.ForeignKey(
                        name: "FK_clients_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO clients (id, full_name, created_at, created_by_user_id)
                SELECT gen_random_uuid(), applicant_name, MIN(submitted_at), NULL
                FROM credit_applications
                GROUP BY applicant_name
                ORDER BY MIN(submitted_at);

                UPDATE credit_applications a
                SET client_id = c.id
                FROM clients c
                WHERE c.full_name = a.applicant_name;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "client_id",
                table: "credit_applications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_client_id",
                table: "credit_applications",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_clients_client_number",
                table: "clients",
                column: "client_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_created_by_user_id",
                table: "clients",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_clients_full_name",
                table: "clients",
                column: "full_name");

            migrationBuilder.AddForeignKey(
                name: "FK_credit_applications_clients_client_id",
                table: "credit_applications",
                column: "client_id",
                principalTable: "clients",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_credit_applications_clients_client_id",
                table: "credit_applications");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropIndex(
                name: "IX_credit_applications_client_id",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "client_id",
                table: "credit_applications");

            migrationBuilder.DropSequence(
                name: "client_number_seq");
        }
    }
}
