using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddOneOpenApplicationPerClient : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE duplicates text;
                BEGIN
                    SELECT string_agg(format('client %s: %s', client_id, refs), '; ') INTO duplicates
                    FROM (
                        SELECT client_id, string_agg(reference, ', ' ORDER BY reference) AS refs
                        FROM credit_applications
                        WHERE stage IN ('Submitted', 'AwaitingSignOff')
                        GROUP BY client_id
                        HAVING count(*) > 1
                    ) open_twice;
                    IF duplicates IS NOT NULL THEN
                        RAISE EXCEPTION 'Clients with more than one open application: %. Decide or withdraw all but one of each, then migrate again.', duplicates;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_credit_applications_client_id",
                table: "credit_applications");

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_client_id_submitted_at",
                table: "credit_applications",
                columns: new[] { "client_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ux_credit_applications_one_open_per_client",
                table: "credit_applications",
                column: "client_id",
                unique: true,
                filter: "stage IN ('Submitted', 'AwaitingSignOff')");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_credit_applications_client_id_submitted_at",
                table: "credit_applications");

            migrationBuilder.DropIndex(
                name: "ux_credit_applications_one_open_per_client",
                table: "credit_applications");

            migrationBuilder.CreateIndex(
                name: "IX_credit_applications_client_id",
                table: "credit_applications",
                column: "client_id");
        }
    }
}
