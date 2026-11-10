# 7. Flows

## 7.1 Sign-in

The Razor login page and `POST /api/auth/login` (React SPA) both call `SignInService`, so the lockout rule has one owner.

```mermaid
sequenceDiagram
    actor U as User
    participant L as Login page or POST /api/auth/login
    participant S as SignInService
    participant DB as PostgreSQL
    U->>L: user name + password
    L->>S: SignInAsync
    S->>DB: find user by normalised user name
    DB-->>S: user with hash, failed count, locked_until
    alt unknown or deactivated user
        S->>S: verify against a throwaway hash (same time as a wrong password)
        S-->>L: InvalidCredentials
    else locked_until in the future
        S-->>L: LockedOut(until), password not checked
    else wrong password
        S->>DB: failed count + 1, the fifth locks for 15 min
        S-->>L: InvalidCredentials, or LockedOut on the fifth
    else correct
        S->>DB: failed count 0, last_signed_in_at = now
        S-->>L: SignedIn(user)
        L-->>U: sign-in cookie with role and security stamp (8 h, HttpOnly, SameSite=Strict), page redirects, API returns MeResponse
    end
    Note over U,L: API failures are 401 problems with code invalid_credentials or locked_out (+ lockedUntil)
```

## 7.1a Every signed-in request

```mermaid
sequenceDiagram
    participant B as Browser
    participant C as Cookie handler (OnValidatePrincipal)
    participant DB as PostgreSQL
    participant E as Endpoint
    B->>C: request with cookie
    C->>DB: read user by id
    alt missing, deactivated, or stamp differs (password reset or changed)
        C-->>B: cookie removed, 401 problem under /api, login page otherwise
    else role or name changed
        C->>C: new principal from the user row, cookie renewed
        C->>E: continue with the new role
    else unchanged
        C->>E: continue
    end
    E->>E: policy = the endpoint's permission (roles from Permissions.For)
    alt role lacks the permission
        E-->>B: 403 problem
    else allowed
        E->>DB: SaveChanges
        Note over E,DB: AuditInterceptor adds one audit_entries row per changed row in the same save (user, action, entity, id, before/after, trace id)
        E-->>B: response
    end
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

## 7.3 Officer decision and second sign-off

Only the assignee (or a manager) decides. An officer's approval against a Reject recommendation or above 25,000 USD waits for a manager.

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
    A->>S: RecordOfficerDecision(application, outcome, reason, actor, actsForBranch)
    alt already decided
        S-->>A: AlreadyDecided
        A-->>P: 409
    else not Submitted (withdrawn, waiting for sign-off)
        S-->>A: NotDecidable
        A-->>P: 409 not_decidable
    else not the assignee and not a manager
        S-->>A: NotAssignee
        A-->>P: 403 not_assignee
    else officer approves against Reject or above 25,000 USD
        S-->>A: SignOffRequested (stage AwaitingSignOff)
        A->>DB: insert sign_off_request, commit
        A-->>P: 201, stage AwaitingSignOff
        Note over A,DB: later a manager calls POST /sign-off: approve creates the officer decision naming both,<br/>return (with a note) sends the application back to Submitted
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

The controller loads plain rows for the range (and, with `from` and `to`, for the previous period of the same length); every number comes from the pure `DashboardCalculator`. A manager's rows are limited to the branch by the query filter.

```mermaid
sequenceDiagram
    actor M as Manager or Risk
    participant D as Dashboard
    participant ST as StatsController
    participant DC as DashboardCalculator
    participant DB as PostgreSQL
    M->>D: open, choose date range (and branch, Risk)
    D->>ST: GET /api/stats?from&to&branchId
    ST->>DB: rows in range: recommendation, P(bad), decision, branch, loan outcome
    ST->>DB: rows in the previous period, shadow scores, branch names
    ST->>DC: Compute(rows, shadows, branches, range, previous)
    DC-->>ST: counts and matrix, override rates, histogram, officers, challenger agreement,<br/>weekly series, by branch, comparison, outcome by decision path, calibration
    ST-->>D: DashboardStats
```

## 7.7 Loan outcome

```mermaid
sequenceDiagram
    actor R as Risk analyst
    participant A as ApplicationsController
    participant DB as PostgreSQL
    R->>A: GET /api/applications?loanOutcome=Pending
    A-->>R: approved loans without an outcome
    R->>A: POST /api/applications/{id}/outcome (Repaid or Defaulted)
    A->>DB: lock row, load application
    alt not decided as approved
        A-->>R: 409 no_loan
    else outcome already recorded
        A-->>R: 409 outcome_recorded
    else
        A->>DB: insert loan_outcome, commit
        A-->>R: 200 with loanOutcome
    end
```
