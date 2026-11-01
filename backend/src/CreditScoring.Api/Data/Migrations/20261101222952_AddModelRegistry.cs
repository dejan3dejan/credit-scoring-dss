using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class AddModelRegistry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scoring_models",
                columns: table => new
                {
                    model_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dataset = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    threshold = table.Column<double>(type: "double precision", nullable: false),
                    role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_models", x => x.model_id);
                    table.CheckConstraint("ck_scoring_models_threshold_range", "threshold > 0 AND threshold < 1");
                    table.ForeignKey(
                        name: "FK_scoring_models_users_registered_by_user_id",
                        column: x => x.registered_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "model_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    from_role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    to_role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_model_events_scoring_models_model_id",
                        column: x => x.model_id,
                        principalTable: "scoring_models",
                        principalColumn: "model_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_model_events_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                ALTER TABLE scoring_models
                    ADD CONSTRAINT ex_scoring_models_one_active
                    EXCLUDE USING btree (role WITH =) WHERE (role = 'Active')
                    DEFERRABLE INITIALLY DEFERRED;
                INSERT INTO scoring_models (model_id, display_name, dataset, threshold, role, registered_at, registered_by_user_id)
                VALUES ('credit_rf_isotonic_v1', 'RandomForest', 'statlog_german_credit', 1.0 / 6.0, 'Active', now(), NULL);
                INSERT INTO model_events (id, model_id, from_role, to_role, reason, changed_by, changed_by_user_id, changed_at)
                VALUES (gen_random_uuid(), 'credit_rf_isotonic_v1', NULL, 'Active', 'Model in use before the registry existed', 'System', NULL, now());
                """);

            migrationBuilder.CreateTable(
                name: "shadow_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    p_bad = table.Column<double>(type: "double precision", nullable: true),
                    threshold_used = table.Column<double>(type: "double precision", nullable: true),
                    recommendation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    scored_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shadow_scores", x => x.id);
                    table.CheckConstraint("ck_shadow_scores_p_bad_range", "p_bad >= 0 AND p_bad <= 1");
                    table.CheckConstraint("ck_shadow_scores_status_fields", "(status = 'Scored' AND p_bad IS NOT NULL AND threshold_used IS NOT NULL AND recommendation IS NOT NULL AND error_code IS NULL) OR (status = 'ScoringUnavailable' AND error_code IS NOT NULL AND p_bad IS NULL AND threshold_used IS NULL AND recommendation IS NULL)");
                    table.ForeignKey(
                        name: "FK_shadow_scores_credit_applications_credit_application_id",
                        column: x => x.credit_application_id,
                        principalTable: "credit_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shadow_scores_scoring_models_model_id",
                        column: x => x.model_id,
                        principalTable: "scoring_models",
                        principalColumn: "model_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_model_events_changed_at",
                table: "model_events",
                column: "changed_at");

            migrationBuilder.CreateIndex(
                name: "IX_model_events_changed_by_user_id",
                table: "model_events",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_model_events_model_id",
                table: "model_events",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "IX_scoring_models_registered_by_user_id",
                table: "scoring_models",
                column: "registered_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shadow_scores_credit_application_id_model_id",
                table: "shadow_scores",
                columns: new[] { "credit_application_id", "model_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shadow_scores_model_id",
                table: "shadow_scores",
                column: "model_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "model_events");

            migrationBuilder.DropTable(
                name: "shadow_scores");

            migrationBuilder.DropTable(
                name: "scoring_models");
        }
    }
}
