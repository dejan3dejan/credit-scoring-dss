# 5. Domain classes

`backend/src/CreditScoring.Api/Domain`. Objects are created through factory methods (`Scored`, `Record`, `Of`, `Create`) that refuse invalid states, so an object that exists is valid.

```mermaid
classDiagram
    direction LR

    class User {
        Guid Id
        string UserName
        string DisplayName
        UserRole Role
        string PasswordHash
        DateTimeOffset CreatedAt
        +Create(userName, displayName, role, createdAt)$ User
        +SetPasswordHash(hash)
        +AsActor() Actor
    }

    class Actor {
        <<record>>
        Guid UserId
        string Name
    }

    class Client {
        Guid Id
        string ClientNumber
        string FullName
        DateOnly? DateOfBirth
        DateTimeOffset CreatedAt
        Guid? CreatedByUserId
        +Create(fullName, dateOfBirth, createdBy, now)$ Client
    }

    class CreditApplication {
        Guid Id
        Guid ClientId
        string ApplicantName
        string SubmittedBy
        Guid? SubmittedByUserId
        DateTimeOffset SubmittedAt
        int LoanAmount
        LoanPurpose Purpose
        EmploymentLength EmploymentLength
        HomeOwnership HomeOwnership
        decimal AnnualIncome
        IncomeVerification IncomeVerification
        decimal DebtToIncomePercent
        int CreditScore
        DateOnly EarliestCreditLine
        int InquiriesLast6Months
        int DelinquenciesLast2Years
        int PublicRecords
        int Bankruptcies
        decimal RevolvingBalance
        decimal RevolvingUtilizationPercent
        int OpenAccounts
        int TotalAccounts
        int MortgageAccounts
    }

    class CreditDecision {
        Guid Id
        DecisionStatus Status
        DateTimeOffset EvaluatedAt
        double? PBad
        string? ModelId
        double? ThresholdUsed
        double? ReviewFromUsed
        Recommendation? Recommendation
        string? ErrorCode
        +Scored(pBad, modelId, threshold, recommendation, at, reviewFrom, factors)$ CreditDecision
        +Unavailable(errorCode, at)$ CreditDecision
    }

    class ScoreFactor {
        Guid Id
        int Rank
        string Input
        double Contribution
        +Of(rank, input, contribution)$ ScoreFactor
    }

    class OfficerDecision {
        Guid Id
        FinalDecision Outcome
        string? Reason
        string DecidedBy
        Guid? DecidedByUserId
        DateTimeOffset DecidedAt
        bool IsOverride
        +IsOverrideOf(recommendation, outcome)$ bool
        +NeedsReason(recommendation, outcome)$ bool
        +Record(recommendation, outcome, reason, by, at)$ OfficerDecision
    }

    class ShadowScore {
        Guid Id
        string ModelId
        DecisionStatus Status
        double? PBad
        double? ThresholdUsed
        double? ReviewFromUsed
        Recommendation? Recommendation
        string? ErrorCode
        DateTimeOffset ScoredAt
        +Of(model, result)$ ShadowScore
    }

    class ScoringFailure {
        Guid Id
        string ErrorCode
        DateTimeOffset AttemptedAt
        +Of(decision)$ ScoringFailure
    }

    class ScoringModel {
        string ModelId
        string DisplayName
        string Dataset
        double Threshold
        double? ReviewFrom
        ModelRole Role
        DateTimeOffset RegisteredAt
        Guid? RegisteredByUserId
        +RecommendationFor(pBad) Recommendation
    }

    class ModelEvent {
        Guid Id
        string ModelId
        ModelRole? FromRole
        ModelRole ToRole
        string? Reason
        string ChangedBy
        DateTimeOffset ChangedAt
    }

    class ModelRegistry {
        <<static>>
        +Register(modelId, name, dataset, threshold, reviewFrom, by, now)$
        +Promote(models, modelId, reason, by, now)$ RegistryResult
        +Retire(models, modelId, reason, by, now)$ RegistryResult
    }

    User ..> Actor : AsActor
    Client "1" <-- "0..*" CreditApplication : for
    CreditApplication "1" *-- "0..1" CreditDecision : scored by
    CreditDecision "1" *-- "0..5" ScoreFactor : explained by
    CreditApplication "1" *-- "0..1" OfficerDecision : decided by
    CreditApplication "1" *-- "0..*" ShadowScore : challenger scores
    CreditApplication "1" *-- "0..*" ScoringFailure : earlier failed tries
    ScoringModel "1" <-- "0..*" ModelEvent : history
    ScoringModel "1" <-- "0..*" ShadowScore : from
    ModelRegistry ..> ScoringModel : changes roles
    ModelRegistry ..> ModelEvent : writes
```

## Enumerations

Stored in the database as their names.

| Enum | Values |
|---|---|
| `Recommendation` | Approve, Reject, Review |
| `FinalDecision` | Approve, Reject |
| `DecisionStatus` | Scored, ScoringUnavailable |
| `ModelRole` | Active, Shadow, Retired |
| `UserRole` | Officer, Manager, Risk |
| `LoanPurpose` | DebtConsolidation, CreditCard, HomeImprovement, MajorPurchase, Car, Medical, Moving, House, Vacation, Wedding, SmallBusiness, RenewableEnergy, Other |
| `EmploymentLength` | NotStated, LessThanOneYear, OneYear to NineYears, TenOrMoreYears |
| `HomeOwnership` | Rent, Own, Mortgage, Other |
| `IncomeVerification` | NotVerified, SourceVerified, Verified |

`SubmittedBy` and `DecidedBy` keep the person's name as it was at the time, next to the user id, so history still reads correctly if a user is renamed.
