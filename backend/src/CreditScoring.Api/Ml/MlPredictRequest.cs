namespace CreditScoring.Api.Ml;

public sealed record MlPredictRequest
{
    public required string CheckingAccountStatus { get; init; }
    public required int DurationMonths { get; init; }
    public required string CreditHistory { get; init; }
    public required string Purpose { get; init; }
    public required int CreditAmount { get; init; }
    public required string SavingsAccount { get; init; }
    public required string EmploymentDuration { get; init; }
    public required int InstallmentRate { get; init; }
    public required string PersonalStatusSex { get; init; }
    public required string OtherDebtorsGuarantors { get; init; }
    public required int PresentResidenceSince { get; init; }
    public required string Property { get; init; }
    public required int Age { get; init; }
    public required string OtherInstallmentPlans { get; init; }
    public required string Housing { get; init; }
    public required int ExistingCreditsCount { get; init; }
    public required string Job { get; init; }
    public required int NumPeopleLiable { get; init; }
    public required string Telephone { get; init; }
    public required string ForeignWorker { get; init; }
}
