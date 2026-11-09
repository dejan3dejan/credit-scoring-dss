using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddApplicationDrafts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answers = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_drafts", x => x.id);
                    table.ForeignKey(
                        name: "FK_application_drafts_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_application_drafts_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_application_drafts_credit_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "credit_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_application_drafts_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_drafts_application_id",
                table: "application_drafts",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_drafts_branch_id",
                table: "application_drafts",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_drafts_client_id",
                table: "application_drafts",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_drafts_created_by_user_id_status",
                table: "application_drafts",
                columns: new[] { "created_by_user_id", "status" });
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_drafts");
        }
    }
}
