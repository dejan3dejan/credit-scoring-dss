using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditScoring.Api.Data.Migrations
{
    public partial class SwitchToLendingClub : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM credit_applications;");

            migrationBuilder.DropColumn(
                name: "duration_months",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "credit_amount",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "age",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "installment_share_percent",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "years_at_residence",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "credits_at_bank",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "dependants",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "checking_account_status",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "credit_history",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "savings_account",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "employment_duration",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "personal_status_sex",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "other_debtors",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "property",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "other_installment_plans",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "housing",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "job",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "has_registered_telephone",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "is_foreign_worker",
                table: "credit_applications");

            migrationBuilder.AddColumn<int>(
                name: "loan_amount",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "employment_length",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "home_ownership",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "annual_income",
                table: "credit_applications",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "income_verification",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "debt_to_income_percent",
                table: "credit_applications",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "credit_score",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "earliest_credit_line",
                table: "credit_applications",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<int>(
                name: "inquiries_last_6_months",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "delinquencies_last_2_years",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "public_records",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "bankruptcies",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "revolving_balance",
                table: "credit_applications",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "revolving_utilization_percent",
                table: "credit_applications",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "open_accounts",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_accounts",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "mortgage_accounts",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "review_from_used",
                table: "credit_decisions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "review_from_used",
                table: "shadow_scores",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "review_from",
                table: "scoring_models",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_credit_decisions_review_band",
                table: "credit_decisions",
                sql: "(review_from_used IS NULL OR (status = 'Scored' AND review_from_used > 0 AND review_from_used < threshold_used)) AND (recommendation IS DISTINCT FROM 'Review' OR review_from_used IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shadow_scores_review_band",
                table: "shadow_scores",
                sql: "(review_from_used IS NULL OR (status = 'Scored' AND review_from_used > 0 AND review_from_used < threshold_used)) AND (recommendation IS DISTINCT FROM 'Review' OR review_from_used IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_scoring_models_review_from_range",
                table: "scoring_models",
                sql: "review_from IS NULL OR (review_from > 0 AND review_from < threshold)");

            migrationBuilder.Sql("""
                INSERT INTO model_events (id, model_id, from_role, to_role, reason, changed_by, changed_by_user_id, changed_at)
                SELECT gen_random_uuid(), model_id, role, 'Retired', 'Replaced by the Lending Club model; German Credit applications cannot be scored by it', 'System', NULL, now()
                FROM scoring_models WHERE role <> 'Retired';
                UPDATE scoring_models SET role = 'Retired' WHERE role <> 'Retired';
                INSERT INTO scoring_models (model_id, display_name, dataset, threshold, review_from, role, registered_at, registered_by_user_id)
                VALUES ('credit_lc_xgb_isotonic_v1', 'XGBoost', 'lending_club', 0.32861227038217605, 0.2, 'Active', now(), NULL);
                INSERT INTO model_events (id, model_id, from_role, to_role, reason, changed_by, changed_by_user_id, changed_at)
                VALUES (gen_random_uuid(), 'credit_lc_xgb_isotonic_v1', NULL, 'Active', 'Lending Club model for the application form', 'System', NULL, now());
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM credit_applications;
                UPDATE scoring_models m SET role = e.from_role
                FROM model_events e
                WHERE e.model_id = m.model_id AND e.to_role = 'Retired' AND e.reason = 'Replaced by the Lending Club model; German Credit applications cannot be scored by it';
                DELETE FROM model_events WHERE reason = 'Replaced by the Lending Club model; German Credit applications cannot be scored by it' OR model_id = 'credit_lc_xgb_isotonic_v1';
                DELETE FROM scoring_models WHERE model_id = 'credit_lc_xgb_isotonic_v1';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_credit_decisions_review_band",
                table: "credit_decisions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shadow_scores_review_band",
                table: "shadow_scores");

            migrationBuilder.DropCheckConstraint(
                name: "ck_scoring_models_review_from_range",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "review_from_used",
                table: "credit_decisions");

            migrationBuilder.DropColumn(
                name: "review_from_used",
                table: "shadow_scores");

            migrationBuilder.DropColumn(
                name: "review_from",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "loan_amount",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "employment_length",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "home_ownership",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "annual_income",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "income_verification",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "debt_to_income_percent",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "credit_score",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "earliest_credit_line",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "inquiries_last_6_months",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "delinquencies_last_2_years",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "public_records",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "bankruptcies",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "revolving_balance",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "revolving_utilization_percent",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "open_accounts",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "total_accounts",
                table: "credit_applications");

            migrationBuilder.DropColumn(
                name: "mortgage_accounts",
                table: "credit_applications");

            migrationBuilder.AddColumn<int>(
                name: "duration_months",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "credit_amount",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "age",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "installment_share_percent",
                table: "credit_applications",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "years_at_residence",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "credits_at_bank",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "dependants",
                table: "credit_applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "checking_account_status",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "credit_history",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "savings_account",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "employment_duration",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "personal_status_sex",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "other_debtors",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "property",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "other_installment_plans",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "housing",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "job",
                table: "credit_applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "has_registered_telephone",
                table: "credit_applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_foreign_worker",
                table: "credit_applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
