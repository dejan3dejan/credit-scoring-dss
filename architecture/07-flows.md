# 7. Flows

## 7.1 Sign-in

```mermaid
sequenceDiagram
    actor U as User
    participant L as Login page
    participant DB as PostgreSQL
    U->>L: user name + password
    L->>DB: find user by user name
    DB-->>L: user with password hash
    alt unknown user or wrong password
        L-->>U: "The user name or password is not correct." (same text for both)
    else correct
        L->>L: PasswordHasher verifies hash
        L-->>U: sign-in cookie (8 h, HttpOnly, SameSite=Strict), redirect
    end
    Note over U,L: Officers and Managers land on New application, Risk on the dashboard
```

## 7.2 Submit and score an application

The main flow. The application is saved whatever happens to scoring, so no officer work is lost.

```mermaid
sequenceDiagram
    actor O as Officer
    participant P as New application page
    participant A as ApplicationsController
    participant S as CreditDecisionService
    participant M as LendingClubMapper
    participant C as MlClient
    participant I as Inference service
    participant DB as PostgreSQL

    O->>P: fills client + 18 answers
    P->>A: POST /api/applications (cookie forwarded)
    A->>A: validate DTO and business limits
    alt invalid
        A-->>P: 400 with field errors
        P-->>O: form with messages next to fields
    else valid
        A->>DB: load client and registered models
        A->>S: EvaluateAsync(application, models)
        S->>M: ToMlRequest(application)
        M-->>S: 18 model inputs (credit history in years, codes)
        par active model
            S->>C: PredictAsync(active model id)
            C->>I: POST /v3/models/{id}/predict
            I-->>C: p_bad, model_id, top_factors
            C-->>S: MlScore or MlFailure
        and each shadow model (2 s budget)
            S->>C: PredictAsync(shadow id)
            C-->>S: MlScore, MlFailure or timeout
        end
        S->>S: RecommendationFor(p_bad): Approve, Review or Reject
        S-->>A: CreditDecision + factors + shadow scores
        A->>DB: save application, decision, factors, shadow scores
        A-->>P: 201 ApplicationResponse
        P-->>O: details page: gauge, recommendation, top factors
    end
```

## 7.3 Officer decision

```mermaid
sequenceDiagram
    actor O as Officer
    participant P as Details page
    participant A as ApplicationsController
    participant S as CreditDecisionService
    participant DB as PostgreSQL

    O->>P: Approve or Reject, optional reason
    P->>A: POST /api/applications/{id}/decision
    A->>DB: begin transaction, lock the application row
    A->>S: RecordOfficerDecision(application, outcome, reason, actor from cookie)
    alt already decided
        S-->>A: AlreadyDecided
        A-->>P: 409
    else reason needed but empty (review case, override, or no score)
        S-->>A: DecisionRefused
        A-->>P: 400 "Give a reason: ..."
    else recorded
        S-->>A: OfficerDecision (IsOverride set)
        A->>DB: insert, commit
        A-->>P: 201
        P-->>O: decision shown, form gone
    end
```

## 7.4 Scoring outage and rescore

```mermaid
sequenceDiagram
    actor O as Officer
    participant A as ApplicationsController
    participant S as CreditDecisionService
    participant I as Inference service
    participant DB as PostgreSQL

    O->>A: submit application
    A->>S: EvaluateAsync
    S-xI: predict (down, slow or bad answer)
    S-->>A: CreditDecision.Unavailable(timeout / unreachable / invalid_response ...)
    A->>DB: save application with status ScoringUnavailable
    A-->>O: "Scoring unavailable", officer may still decide with a reason
    Note over O,I: later, when the service is back
    O->>A: POST /api/applications/{id}/rescore
    A->>S: RescoreAsync (refused if already scored or decided)
    S->>I: predict
    I-->>S: p_bad, factors
    A->>DB: lock row, recheck, move old failure to scoring_failures, save new decision, commit
    A-->>O: 200 with the new score
```

## 7.5 Model promotion (champion and challenger)

```mermaid
sequenceDiagram
    actor R as Risk analyst
    participant MP as Model page
    participant MC as ModelsController
    participant I as Inference service
    participant REG as ModelRegistry
    participant DB as PostgreSQL

    R->>MP: open registry
    MP->>MC: GET /api/models/available
    MC->>I: GET /v3/models
    I-->>MC: loaded models with threshold and review band
    R->>MP: register a new model
    MP->>MC: POST /api/models
    MC->>REG: Register: new model starts as Shadow
    MC->>DB: insert model + event
    Note over R,DB: shadow model now scores every new application in the background
    R->>MP: promote, with a reason
    MP->>MC: POST /api/models/{id}/promote
    MC->>REG: Promote: old Active becomes Shadow, target becomes Active
    REG-->>MC: two ModelEvents
    MC->>DB: save roles + events (EXCLUDE constraint keeps one Active)
    MC-->>MP: updated list
```

## 7.6 Dashboard

```mermaid
sequenceDiagram
    actor M as Manager or Risk
    participant D as Dashboard page
    participant ST as StatsController
    participant DC as DashboardCalculator
    participant DB as PostgreSQL
    M->>D: open, choose date range
    D->>ST: GET /api/stats?from&to
    ST->>DB: load applications in range (UTC) with decisions and shadow scores
    ST->>DC: Compute(rows, shadows)
    DC-->>ST: counts by recommendation and outcome, override rates (per officer),<br/>unavailable rate, P(bad) histogram, champion and challenger agreement
    ST-->>D: DashboardStats
    D-->>M: tiles, bars and histogram (inline SVG, no chart library)
```
