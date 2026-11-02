using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;

namespace CreditScoring.Tests;

internal static class TestData
{
    public const string ModelId = "credit_lc_xgb_isotonic_v1";
    public const double Threshold = 1 / (1 + 2.0431);
    public const double ReviewFrom = 0.2;
    public const string Dataset = "lending_club";

    public static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public static readonly Actor Officer = new(Guid.Parse("0192f000-0000-7000-8000-000000000001"), "Tester");

    public static readonly Client Client = Client.Create("Test Applicant", null, Officer, Now);

    public static ScoringModel ShadowModel(string modelId, double threshold = Threshold, double? reviewFrom = ReviewFrom) =>
        ModelRegistry.Register(modelId, "Test model", Dataset, threshold, reviewFrom, Officer, Now).Model;

    public static ScoringModel ActiveModel(string modelId = ModelId, double threshold = Threshold, double? reviewFrom = ReviewFrom)
    {
        var model = ShadowModel(modelId, threshold, reviewFrom);
        ModelRegistry.Promote([model], modelId, "test", Officer, Now);
        return model;
    }

    public static CreateApplicationRequest ValidRequest() => new()
    {
        ClientId = Client.Id,
        LoanAmount = 12_000,
        Purpose = LoanPurpose.DebtConsolidation,
        EmploymentLength = EmploymentLength.FourYears,
        HomeOwnership = HomeOwnership.Rent,
        AnnualIncome = 58_000m,
        IncomeVerification = IncomeVerification.SourceVerified,
        DebtToIncomePercent = 18.4m,
        CreditScore = 697,
        EarliestCreditLine = new DateOnly(2015, 5, 1),
        InquiriesLast6Months = 1,
        DelinquenciesLast2Years = 0,
        PublicRecords = 0,
        Bankruptcies = 0,
        RevolvingBalance = 9_400m,
        RevolvingUtilizationPercent = 52.3m,
        OpenAccounts = 9,
        TotalAccounts = 21,
        MortgageAccounts = 0,
    };

    public static readonly MlPredictRequest ContractRequest = new()
    {
        LoanAmnt = 12_000,
        Purpose = "debt_consolidation",
        EmpLengthYears = 4,
        HomeOwnership = "RENT",
        AnnualInc = 58_000,
        VerificationStatus = "Source Verified",
        Dti = 18.4,
        Fico = 697,
        CreditHistoryYears = 4202 / 365.25,
        InqLast6mths = 1,
        Delinq2yrs = 0,
        PubRec = 0,
        PubRecBankruptcies = 0,
        RevolBal = 9_400,
        RevolUtil = 52.3,
        OpenAcc = 9,
        TotalAcc = 21,
        MortAcc = 0,
    };

    public static CreditApplication ContractApplication() => new()
    {
        ApplicantName = "Test Applicant",
        ClientId = Client.Id,
        Client = Client,
        SubmittedBy = "tester",
        SubmittedAt = Now,
        LoanAmount = 12_000,
        Purpose = LoanPurpose.DebtConsolidation,
        EmploymentLength = EmploymentLength.FourYears,
        HomeOwnership = HomeOwnership.Rent,
        AnnualIncome = 58_000m,
        IncomeVerification = IncomeVerification.SourceVerified,
        DebtToIncomePercent = 18.4m,
        CreditScore = 697,
        EarliestCreditLine = new DateOnly(2015, 5, 1),
        InquiriesLast6Months = 1,
        DelinquenciesLast2Years = 0,
        PublicRecords = 0,
        Bankruptcies = 0,
        RevolvingBalance = 9_400m,
        RevolvingUtilizationPercent = 52.3m,
        OpenAccounts = 9,
        TotalAccounts = 21,
        MortgageAccounts = 0,
    };
}
