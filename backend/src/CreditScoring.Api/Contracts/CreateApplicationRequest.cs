using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record CreateApplicationRequest
{
    [Required, StringLength(BusinessLimits.ApplicantNameMaxLength)]
    public string? ApplicantName { get; init; }

    [Required, StringLength(BusinessLimits.SubmittedByMaxLength)]
    public string? SubmittedBy { get; init; }

    [Required, Range(BusinessLimits.MinDurationMonths, BusinessLimits.MaxDurationMonths)]
    public int? DurationMonths { get; init; }

    [Required, Range(BusinessLimits.MinCreditAmount, BusinessLimits.MaxCreditAmount)]
    public int? CreditAmount { get; init; }

    [Required, Range(BusinessLimits.MinAge, BusinessLimits.MaxAge)]
    public int? Age { get; init; }

    [Required, Range(BusinessLimits.MinInstallmentSharePercent, BusinessLimits.MaxInstallmentSharePercent)]
    public decimal? InstallmentSharePercent { get; init; }

    [Required, Range(0, BusinessLimits.MaxAge), NotGreaterThan(nameof(Age))]
    public int? YearsAtResidence { get; init; }

    [Required, Range(BusinessLimits.MinCreditsAtBank, BusinessLimits.MaxCreditsAtBank)]
    public int? CreditsAtBank { get; init; }

    [Required, Range(0, BusinessLimits.MaxDependants)]
    public int? Dependants { get; init; }

    [Required, EnumDataType(typeof(CheckingAccountStatus))]
    public CheckingAccountStatus? CheckingAccountStatus { get; init; }

    [Required, EnumDataType(typeof(CreditHistory))]
    public CreditHistory? CreditHistory { get; init; }

    [Required, EnumDataType(typeof(LoanPurpose))]
    public LoanPurpose? Purpose { get; init; }

    [Required, EnumDataType(typeof(SavingsAccount))]
    public SavingsAccount? SavingsAccount { get; init; }

    [Required, EnumDataType(typeof(EmploymentDuration))]
    public EmploymentDuration? EmploymentDuration { get; init; }

    [Required, EnumDataType(typeof(PersonalStatusSex))]
    public PersonalStatusSex? PersonalStatusSex { get; init; }

    [Required, EnumDataType(typeof(OtherDebtors))]
    public OtherDebtors? OtherDebtors { get; init; }

    [Required, EnumDataType(typeof(PropertyType))]
    public PropertyType? Property { get; init; }

    [Required, EnumDataType(typeof(OtherInstallmentPlans))]
    public OtherInstallmentPlans? OtherInstallmentPlans { get; init; }

    [Required, EnumDataType(typeof(Housing))]
    public Housing? Housing { get; init; }

    [Required, EnumDataType(typeof(JobType))]
    public JobType? Job { get; init; }

    [Required]
    public bool? HasRegisteredTelephone { get; init; }

    [Required]
    public bool? IsForeignWorker { get; init; }

    public CreditApplication ToDomain(DateTimeOffset submittedAt) => new()
    {
        ApplicantName = ApplicantName!.Trim(),
        SubmittedBy = SubmittedBy!.Trim(),
        SubmittedAt = submittedAt,
        DurationMonths = DurationMonths!.Value,
        CreditAmount = CreditAmount!.Value,
        Age = Age!.Value,
        InstallmentSharePercent = InstallmentSharePercent!.Value,
        YearsAtResidence = YearsAtResidence!.Value,
        CreditsAtBank = CreditsAtBank!.Value,
        Dependants = Dependants!.Value,
        CheckingAccountStatus = CheckingAccountStatus!.Value,
        CreditHistory = CreditHistory!.Value,
        Purpose = Purpose!.Value,
        SavingsAccount = SavingsAccount!.Value,
        EmploymentDuration = EmploymentDuration!.Value,
        PersonalStatusSex = PersonalStatusSex!.Value,
        OtherDebtors = OtherDebtors!.Value,
        Property = Property!.Value,
        OtherInstallmentPlans = OtherInstallmentPlans!.Value,
        Housing = Housing!.Value,
        Job = Job!.Value,
        HasRegisteredTelephone = HasRegisteredTelephone!.Value,
        IsForeignWorker = IsForeignWorker!.Value,
    };
}
