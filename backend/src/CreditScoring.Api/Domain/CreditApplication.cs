namespace CreditScoring.Api.Domain;

public class CreditApplication
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; init; }
    public Client? Client { get; set; }
    public required string ApplicantName { get; init; }
    public required string SubmittedBy { get; init; }
    public Guid? SubmittedByUserId { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }

    public required int LoanAmount { get; init; }
    public required LoanPurpose Purpose { get; init; }
    public required EmploymentLength EmploymentLength { get; init; }
    public required HomeOwnership HomeOwnership { get; init; }
    public required decimal AnnualIncome { get; init; }
    public required IncomeVerification IncomeVerification { get; init; }
    public required decimal DebtToIncomePercent { get; init; }
    public required int CreditScore { get; init; }
    public required DateOnly EarliestCreditLine { get; init; }
    public required int InquiriesLast6Months { get; init; }
    public required int DelinquenciesLast2Years { get; init; }
    public required int PublicRecords { get; init; }
    public required int Bankruptcies { get; init; }
    public required decimal RevolvingBalance { get; init; }
    public required decimal RevolvingUtilizationPercent { get; init; }
    public required int OpenAccounts { get; init; }
    public required int TotalAccounts { get; init; }
    public required int MortgageAccounts { get; init; }

    public CreditDecision? Decision { get; set; }
    public OfficerDecision? OfficerDecision { get; set; }
    public List<ShadowScore> ShadowScores { get; init; } = [];
    public List<ScoringFailure> ScoringFailures { get; init; } = [];
}
