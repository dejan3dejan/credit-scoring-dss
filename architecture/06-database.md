# 6. Database

PostgreSQL, created by EF Core migrations. Enums are stored as their names (text), so rows read without a lookup table. Normalised: each fact is stored once, and the history and dashboard are queries over these tables, not copies.

```mermaid
erDiagram
    users {
        uuid id PK
        text user_name UK
        text display_name
        text role
        text password_hash
        timestamptz created_at
        text preferred_language "en or sr"
        int failed_sign_in_count
        timestamptz locked_until
        timestamptz last_signed_in_at
        text email
        bool is_active
        bool must_change_password
        uuid security_stamp
        uuid branch_id FK "Officer and Manager only"
    }
    branches {
        uuid id PK
        text code UK
        text name
        text city
        bool is_active
        timestamptz created_at
    }
    audit_entries {
        uuid id PK
        timestamptz at
        uuid user_id "no FK"
        text user_name
        text action
        text entity
        text entity_id
        text trace_id
        jsonb changes
    }
    clients {
        uuid id PK
        text client_number UK
        text full_name
        date date_of_birth
        text address
        text phone
        text email
        text employer
        uuid branch_id FK
        bool is_active
        timestamptz anonymised_at
        timestamptz created_at
        uuid created_by_user_id FK
    }
    credit_applications {
        uuid id PK
        uuid client_id FK
        uuid branch_id FK
        text reference UK "A-000001"
        text stage
        uuid assigned_to_user_id FK
        timestamptz withdrawn_at
        text withdrawn_by
        text withdrawal_reason
        text applicant_name
        text submitted_by
        uuid submitted_by_user_id FK
        timestamptz submitted_at
        int loan_amount
        text purpose
        text employment_length
        text home_ownership
        numeric annual_income
        text income_verification
        numeric debt_to_income_percent
        int credit_score
        date earliest_credit_line
        int inquiries_last_6_months
        int delinquencies_last_2_years
        int public_records
        int bankruptcies
        numeric revolving_balance
        numeric revolving_utilization_percent
        int open_accounts
        int total_accounts
        int mortgage_accounts
    }
    credit_decisions {
        uuid id PK
        uuid credit_application_id FK
        text status
        timestamptz evaluated_at
        float p_bad
        text model_id
        float threshold_used
        float review_from_used
        text recommendation
        text error_code
    }
    score_factors {
        uuid id PK
        uuid credit_decision_id FK
        int rank
        text input
        float contribution
    }
    officer_decisions {
        uuid id PK
        uuid credit_application_id FK
        text outcome
        text reason
        text decided_by
        uuid decided_by_user_id FK
        timestamptz decided_at
        bool is_override
        text signed_off_by
        uuid signed_off_by_user_id FK
        timestamptz signed_off_at
    }
    sign_off_requests {
        uuid id PK
        uuid credit_application_id FK
        text outcome
        text reason
        text why
        text requested_by
        timestamptz requested_at
        text status
        text reviewed_by
        timestamptz reviewed_at
        text note
    }
    loan_outcomes {
        uuid id PK
        uuid credit_application_id FK "unique"
        text outcome
        text note
        text recorded_by
        uuid recorded_by_user_id FK
        timestamptz recorded_at
    }
    application_notes {
        uuid id PK
        uuid credit_application_id FK
        text text
        text author
        timestamptz created_at
    }
    application_drafts {
        uuid id PK
        uuid client_id FK
        uuid branch_id FK
        jsonb answers
        text status
        uuid application_id FK
        uuid created_by_user_id FK
        timestamptz updated_at
    }
    shadow_scores {
        uuid id PK
        uuid credit_application_id FK
        text model_id FK
        text status
        float p_bad
        float threshold_used
        float review_from_used
        text recommendation
        text error_code
        timestamptz scored_at
    }
    scoring_failures {
        uuid id PK
        uuid credit_application_id FK
        text error_code
        timestamptz attempted_at
    }
    scoring_models {
        text model_id PK
        text display_name
        text dataset
        float threshold
        float review_from
        text role
        timestamptz registered_at
        uuid registered_by_user_id FK
    }
    model_events {
        uuid id PK
        text model_id FK
        text from_role
        text to_role
        text reason
        text changed_by
        uuid changed_by_user_id FK
        timestamptz changed_at
    }

    branches ||--o{ users : "staff"
    branches ||--o{ clients : "serves"
    branches ||--o{ credit_applications : "handled at"
    clients ||--o{ credit_applications : "has"
    credit_applications ||--o| credit_decisions : "scored as"
    credit_decisions ||--o{ score_factors : "top 5"
    credit_applications ||--o| officer_decisions : "decided as"
    credit_applications ||--o{ shadow_scores : "challenger scores"
    credit_applications ||--o{ scoring_failures : "failed tries"
    credit_applications ||--o{ sign_off_requests : "second sign-off"
    credit_applications ||--o{ application_notes : "notes"
    credit_applications ||--o| loan_outcomes : "outcome"
    clients ||--o{ application_drafts : "drafts"
    application_drafts |o--o| credit_applications : "submitted as"
    scoring_models ||--o{ shadow_scores : "scored with"
    scoring_models ||--o{ model_events : "history"
    users ||--o{ clients : "created"
    users ||--o{ credit_applications : "submitted"
    users ||--o{ officer_decisions : "decided"
    users ||--o{ scoring_models : "registered"
    users ||--o{ model_events : "changed"
```

## Rules the database itself enforces

| Table | Rule |
|---|---|
| `scoring_models` | threshold strictly between 0 and 1; review edge, if set, between 0 and the threshold; at most one `Active` model (an `EXCLUDE` constraint, so two promotions at once cannot both win) |
| `credit_decisions`, `shadow_scores` | probability between 0 and 1; a scored row has a model and threshold, an unavailable row has an error code; a `Review` recommendation needs its review edge |
| `score_factors` | rank 1 or more, unique per decision |
| `officer_decisions` | an override must have a reason |
| `sign_off_requests` | at most one pending request per application (partial unique index); a request is reviewed exactly when it is no longer pending |
| `credit_applications` | unique reference from `application_reference_seq`; existing rows numbered in submission order; at most one open application (`Submitted` or `AwaitingSignOff`) per client (partial unique index) |
| `clients`, `users`, `branches` | PostgreSQL's `xmin` serves as the row version for optimistic concurrency on edits |
| `shadow_scores` | one score per application and model |
| all user foreign keys | `RESTRICT`: a user with history cannot be deleted |
| `users` | language `en` or `sr`; failed count not negative; existing users migrated as active, each with its own random security stamp; a branch exactly when the role is Officer or Manager |
| `branches` | unique code; `HQ` created by the migration, which moved existing officers, managers, clients and applications to it |
| `clients`, `credit_applications` | a branch is required (`RESTRICT`) |
| `audit_entries` | no foreign keys, so the log does not depend on the rows it describes; written only by the `SaveChanges` interceptor |
| application children | `CASCADE`: decisions, factors and scores belong to their application |

Indexes support the history page (submission date, applicant name), the dashboard (status and recommendation, override and outcome), the model event log (change date) and the audit log (time, entity and id, user).
