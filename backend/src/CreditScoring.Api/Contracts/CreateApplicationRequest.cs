using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record CreateApplicationRequest
{
    [Display(Name = "Draft")]
    public Guid? DraftId { get; init; }

    [Required, Display(Name = "Client")]
    public Guid? ClientId { get; init; }

    [Required, Range(BusinessLimits.MinLoanAmount, BusinessLimits.MaxLoanAmount), Display(Name = "Loan amount (USD)")]
    public int? LoanAmount { get; init; }

    [Required, EnumDataType(typeof(LoanPurpose)), Display(Name = "Purpose")]
    public LoanPurpose? Purpose { get; init; }

    [Required, EnumDataType(typeof(EmploymentLength)), Display(Name = "Years in current job")]
    public EmploymentLength? EmploymentLength { get; init; }

    [Required, EnumDataType(typeof(HomeOwnership)), Display(Name = "Housing")]
    public HomeOwnership? HomeOwnership { get; init; }

    [Required, Range(0, BusinessLimits.MaxAnnualIncome), Display(Name = "Annual income (USD)")]
    public decimal? AnnualIncome { get; init; }

    [Required, EnumDataType(typeof(IncomeVerification)), Display(Name = "Income verification")]
    public IncomeVerification? IncomeVerification { get; init; }

    [Required, Range(0, BusinessLimits.MaxDebtToIncomePercent), Display(Name = "Debt-to-income ratio (%)")]
    public decimal? DebtToIncomePercent { get; init; }

    [Required, Range(BusinessLimits.MinCreditScore, BusinessLimits.MaxCreditScore), Display(Name = "Credit score")]
    public int? CreditScore { get; init; }

    [Required, Display(Name = "First credit line opened")]
    public DateOnly? EarliestCreditLine { get; init; }

    [Required, Range(0, BusinessLimits.MaxRecentCount), Display(Name = "Credit inquiries, last 6 months")]
    public int? InquiriesLast6Months { get; init; }

    [Required, Range(0, BusinessLimits.MaxRecentCount), Display(Name = "Delinquencies, last 2 years")]
    public int? DelinquenciesLast2Years { get; init; }

    [Required, Range(0, BusinessLimits.MaxRecentCount), Display(Name = "Derogatory public records")]
    public int? PublicRecords { get; init; }

    [Required, Range(0, BusinessLimits.MaxRecentCount), NotGreaterThan(nameof(PublicRecords)), Display(Name = "Bankruptcies")]
    public int? Bankruptcies { get; init; }

    [Required, Range(0, BusinessLimits.MaxRevolvingBalance), Display(Name = "Revolving balance (USD)")]
    public decimal? RevolvingBalance { get; init; }

    [Required, Range(0, BusinessLimits.MaxRevolvingUtilizationPercent), MustBeZeroWhen(nameof(RevolvingBalance)), Display(Name = "Revolving utilisation (%)")]
    public decimal? RevolvingUtilizationPercent { get; init; }

    [Required, Range(0, BusinessLimits.MaxOpenAccounts), NotGreaterThan(nameof(TotalAccounts)), Display(Name = "Open credit lines")]
    public int? OpenAccounts { get; init; }

    [Required, Range(0, BusinessLimits.MaxTotalAccounts), Display(Name = "Credit lines ever opened")]
    public int? TotalAccounts { get; init; }

    [Required, Range(0, BusinessLimits.MaxRecentCount), NotGreaterThan(nameof(TotalAccounts)), Display(Name = "Mortgage accounts")]
    public int? MortgageAccounts { get; init; }

    public CreditApplication ToDomain(DateTimeOffset submittedAt, Actor submittedBy, Client client) => new()
    {
        ClientId = client.Id,
        Client = client,
        BranchId = client.BranchId,
        ApplicantName = client.FullName,
        SubmittedBy = submittedBy.Name,
        SubmittedByUserId = submittedBy.UserId,
        SubmittedAt = submittedAt,
        LoanAmount = LoanAmount!.Value,
        Purpose = Purpose!.Value,
        EmploymentLength = EmploymentLength!.Value,
        HomeOwnership = HomeOwnership!.Value,
        AnnualIncome = AnnualIncome!.Value,
        IncomeVerification = IncomeVerification!.Value,
        DebtToIncomePercent = DebtToIncomePercent!.Value,
        CreditScore = CreditScore!.Value,
        EarliestCreditLine = EarliestCreditLine!.Value,
        InquiriesLast6Months = InquiriesLast6Months!.Value,
        DelinquenciesLast2Years = DelinquenciesLast2Years!.Value,
        PublicRecords = PublicRecords!.Value,
        Bankruptcies = Bankruptcies!.Value,
        RevolvingBalance = RevolvingBalance!.Value,
        RevolvingUtilizationPercent = RevolvingUtilizationPercent!.Value,
        OpenAccounts = OpenAccounts!.Value,
        TotalAccounts = TotalAccounts!.Value,
        MortgageAccounts = MortgageAccounts!.Value,
    };

    public static CreateApplicationRequest From(ApplicationResponse previous) => new()
    {
        ClientId = previous.ClientId,
        LoanAmount = previous.LoanAmount,
        Purpose = previous.Purpose,
        EmploymentLength = previous.EmploymentLength,
        HomeOwnership = previous.HomeOwnership,
        AnnualIncome = previous.AnnualIncome,
        IncomeVerification = previous.IncomeVerification,
        DebtToIncomePercent = previous.DebtToIncomePercent,
        CreditScore = previous.CreditScore,
        EarliestCreditLine = previous.EarliestCreditLine,
        InquiriesLast6Months = previous.InquiriesLast6Months,
        DelinquenciesLast2Years = previous.DelinquenciesLast2Years,
        PublicRecords = previous.PublicRecords,
        Bankruptcies = previous.Bankruptcies,
        RevolvingBalance = previous.RevolvingBalance,
        RevolvingUtilizationPercent = previous.RevolvingUtilizationPercent,
        OpenAccounts = previous.OpenAccounts,
        TotalAccounts = previous.TotalAccounts,
        MortgageAccounts = previous.MortgageAccounts,
    };
}
