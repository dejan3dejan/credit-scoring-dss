using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record CreateApplicationRequest
{
    [Required, StringLength(BusinessLimits.ApplicantNameMaxLength), Display(Name = "Applicant name")]
    public string? ApplicantName { get; init; }

    [Required, StringLength(BusinessLimits.SubmittedByMaxLength), Display(Name = "Submitted by")]
    public string? SubmittedBy { get; init; }

    [Required, Range(BusinessLimits.MinDurationMonths, BusinessLimits.MaxDurationMonths), Display(Name = "Loan term (months)")]
    public int? DurationMonths { get; init; }

    [Required, Range(BusinessLimits.MinCreditAmount, BusinessLimits.MaxCreditAmount), Display(Name = "Credit amount (DM)")]
    public int? CreditAmount { get; init; }

    [Required, Range(BusinessLimits.MinAge, BusinessLimits.MaxAge), Display(Name = "Age")]
    public int? Age { get; init; }

    [Required, Range(BusinessLimits.MinInstallmentSharePercent, BusinessLimits.MaxInstallmentSharePercent), Display(Name = "Installment share of disposable income (%)")]
    public decimal? InstallmentSharePercent { get; init; }

    [Required, Range(0, BusinessLimits.MaxAge), NotGreaterThan(nameof(Age)), Display(Name = "Years at current residence")]
    public int? YearsAtResidence { get; init; }

    [Required, Range(BusinessLimits.MinCreditsAtBank, BusinessLimits.MaxCreditsAtBank), Display(Name = "Credits at this bank")]
    public int? CreditsAtBank { get; init; }

    [Required, Range(0, BusinessLimits.MaxDependants), Display(Name = "Dependants")]
    public int? Dependants { get; init; }

    [Required, EnumDataType(typeof(CheckingAccountStatus)), Display(Name = "Checking account")]
    public CheckingAccountStatus? CheckingAccountStatus { get; init; }

    [Required, EnumDataType(typeof(CreditHistory)), Display(Name = "Credit history")]
    public CreditHistory? CreditHistory { get; init; }

    [Required, EnumDataType(typeof(LoanPurpose)), Display(Name = "Purpose")]
    public LoanPurpose? Purpose { get; init; }

    [Required, EnumDataType(typeof(SavingsAccount)), Display(Name = "Savings")]
    public SavingsAccount? SavingsAccount { get; init; }

    [Required, EnumDataType(typeof(EmploymentDuration)), Display(Name = "Current employment")]
    public EmploymentDuration? EmploymentDuration { get; init; }

    [Required, EnumDataType(typeof(PersonalStatusSex)), Display(Name = "Personal status and sex")]
    public PersonalStatusSex? PersonalStatusSex { get; init; }

    [Required, EnumDataType(typeof(OtherDebtors)), Display(Name = "Other debtors or guarantors")]
    public OtherDebtors? OtherDebtors { get; init; }

    [Required, EnumDataType(typeof(PropertyType)), Display(Name = "Most valuable property")]
    public PropertyType? Property { get; init; }

    [Required, EnumDataType(typeof(OtherInstallmentPlans)), Display(Name = "Other installment plans")]
    public OtherInstallmentPlans? OtherInstallmentPlans { get; init; }

    [Required, EnumDataType(typeof(Housing)), Display(Name = "Housing")]
    public Housing? Housing { get; init; }

    [Required, EnumDataType(typeof(JobType)), Display(Name = "Job")]
    public JobType? Job { get; init; }

    [Required, Display(Name = "Telephone in own name")]
    public bool? HasRegisteredTelephone { get; init; }

    [Required, Display(Name = "Foreign worker")]
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
