# 8. Decision logic and states

## 8.1 Recommendation rule

`ScoringModel.RecommendationFor(pBad)`. The edges belong to the registered model, so each stored decision records the threshold and review edge it was made with.

```mermaid
flowchart TD
    start(["P(bad) from the active model"]) --> rej{"P(bad) >= threshold<br/>0.3286"}
    rej -- yes --> R["Reject"]
    rej -- no --> band{"review edge set<br/>and P(bad) >= 0.20"}
    band -- yes --> V["Review closely"]
    band -- no --> A["Approve"]
```

Where the numbers come from:

- **Threshold 0.3286** = 1 / (1 + 2.0431). On training loans a charged-off loan lost $4,203.69 on average and a fully paid one earned $2,057.47, so a false approval costs 2.0431 times a false rejection. Rejecting pays when P(bad) is above this point.
- **Review edge 0.20** was chosen in cross-validation (LC-03) before the test year was scored: about one application in six goes to a person, and that group holds about 30% of the bad loans.

## 8.2 When the officer must write a reason

`OfficerDecision.NeedsReason(recommendation, outcome)`. An override is only a decision against a clear recommendation; a review case has none, but still needs a reason.

```mermaid
flowchart TD
    s(["Officer chooses Approve or Reject"]) --> none{"was a recommendation made?"}
    none -- "no (scoring unavailable)" --> need["Reason required"]
    none -- yes --> rev{"recommendation = Review?"}
    rev -- yes --> need
    rev -- no --> diff{"outcome differs from<br/>Approve or Reject recommendation?"}
    diff -- "yes: override" --> need2["Reason required,<br/>IsOverride = true"]
    diff -- no --> ok["Reason optional"]
```

## 8.3 Application life cycle

```mermaid
stateDiagram-v2
    [*] --> Submitted: officer submits
    Submitted --> Scored: model answers
    Submitted --> ScoringUnavailable: timeout, unreachable,<br/>bad answer, no active model
    ScoringUnavailable --> Scored: rescore succeeds<br/>(old failure kept)
    ScoringUnavailable --> ScoringUnavailable: rescore fails again
    Scored --> Decided: officer approves or rejects
    ScoringUnavailable --> Decided: officer decides with a reason
    Decided --> [*]
    note right of Decided
        final: no edits, no rescore
    end note
```

`Submitted` is not stored: an application is saved together with its first scoring result in one step.

## 8.4 Model life cycle

```mermaid
stateDiagram-v2
    [*] --> Shadow: registered by Risk
    Shadow --> Active: promoted, with reason
    Active --> Shadow: another model promoted
    Shadow --> Retired: retired, with reason
    Retired --> [*]
    note right of Active
        exactly one at a time;
        an Active model cannot be retired directly
    end note
```

Every arrow writes a `ModelEvent` with who, when and why.
