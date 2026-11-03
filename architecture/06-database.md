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
    }
    clients {
        uuid id PK
        text client_number UK
        text full_name
        date date_of_birth
        timestamptz created_at
        uuid created_by_user_id FK
    }
    credit_applications {
        uuid id PK
        uuid client_id FK
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

    clients ||--o{ credit_applications : "has"
    credit_applications ||--o| credit_decisions : "scored as"
    credit_decisions ||--o{ score_factors : "top 5"
    credit_applications ||--o| officer_decisions : "decided as"
    credit_applications ||--o{ shadow_scores : "challenger scores"
    credit_applications ||--o{ scoring_failures : "failed tries"
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
| `shadow_scores` | one score per application and model |
| all user foreign keys | `RESTRICT`: a user with history cannot be deleted |
| application children | `CASCADE`: decisions, factors and scores belong to their application |

Indexes support the history page (submission date, applicant name), the dashboard (status and recommendation, override and outcome) and the model event log (change date).
