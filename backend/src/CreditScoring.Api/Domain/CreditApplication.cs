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

    public required int DurationMonths { get; init; }
    public required int CreditAmount { get; init; }
    public required int Age { get; init; }
    public required decimal InstallmentSharePercent { get; init; }
    public required int YearsAtResidence { get; init; }
    public required int CreditsAtBank { get; init; }
    public required int Dependants { get; init; }

    public required CheckingAccountStatus CheckingAccountStatus { get; init; }
    public required CreditHistory CreditHistory { get; init; }
    public required LoanPurpose Purpose { get; init; }
    public required SavingsAccount SavingsAccount { get; init; }
    public required EmploymentDuration EmploymentDuration { get; init; }
    public required PersonalStatusSex PersonalStatusSex { get; init; }
    public required OtherDebtors OtherDebtors { get; init; }
    public required PropertyType Property { get; init; }
    public required OtherInstallmentPlans OtherInstallmentPlans { get; init; }
    public required Housing Housing { get; init; }
    public required JobType Job { get; init; }
    public required bool HasRegisteredTelephone { get; init; }
    public required bool IsForeignWorker { get; init; }

    public CreditDecision? Decision { get; set; }
    public OfficerDecision? OfficerDecision { get; set; }
}
