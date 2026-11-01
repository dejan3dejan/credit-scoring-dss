using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record CreateApplicationRequest
{
    [Required, Display(Name = "Client")]
    public Guid? ClientId { get; init; }

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

    public CreditApplication ToDomain(DateTimeOffset submittedAt, Actor submittedBy, Client client) => new()
    {
        ClientId = client.Id,
        Client = client,
        ApplicantName = client.FullName,
        SubmittedBy = submittedBy.Name,
        SubmittedByUserId = submittedBy.UserId,
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

    public static CreateApplicationRequest From(ApplicationResponse previous) => new()
    {
        ClientId = previous.ClientId,
        DurationMonths = previous.DurationMonths,
        CreditAmount = previous.CreditAmount,
        Age = previous.Age,
        InstallmentSharePercent = previous.InstallmentSharePercent,
        YearsAtResidence = previous.YearsAtResidence,
        CreditsAtBank = previous.CreditsAtBank,
        Dependants = previous.Dependants,
        CheckingAccountStatus = previous.CheckingAccountStatus,
        CreditHistory = previous.CreditHistory,
        Purpose = previous.Purpose,
        SavingsAccount = previous.SavingsAccount,
        EmploymentDuration = previous.EmploymentDuration,
        PersonalStatusSex = previous.PersonalStatusSex,
        OtherDebtors = previous.OtherDebtors,
        Property = previous.Property,
        OtherInstallmentPlans = previous.OtherInstallmentPlans,
        Housing = previous.Housing,
        Job = previous.Job,
        HasRegisteredTelephone = previous.HasRegisteredTelephone,
        IsForeignWorker = previous.IsForeignWorker,
    };
}
