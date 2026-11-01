using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;

namespace CreditScoring.Tests;

internal static class TestData
{
    public const string ModelId = "credit_rf_isotonic_v1";
    public const double Threshold = 1.0 / 6.0;
    public const string Dataset = "statlog_german_credit";

    public static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public static readonly Actor Officer = new(Guid.Parse("0192f000-0000-7000-8000-000000000001"), "Tester");

    public static readonly Client Client = Client.Create("Test Applicant", null, Officer, Now);

    public static ScoringModel ShadowModel(string modelId, double threshold = Threshold) =>
        ModelRegistry.Register(modelId, "Test model", Dataset, threshold, Officer, Now).Model;

    public static ScoringModel ActiveModel(string modelId = ModelId, double threshold = Threshold)
    {
        var model = ShadowModel(modelId, threshold);
        ModelRegistry.Promote([model], modelId, "test", Officer, Now);
        return model;
    }

    public static CreateApplicationRequest ValidRequest() => new()
    {
        ClientId = Client.Id,
        DurationMonths = 24,
        CreditAmount = 3500,
        Age = 35,
        InstallmentSharePercent = 22m,
        YearsAtResidence = 2,
        CreditsAtBank = 1,
        Dependants = 0,
        CheckingAccountStatus = CheckingAccountStatus.Negative,
        CreditHistory = CreditHistory.NoCreditsOrAllPaidDuly,
        Purpose = LoanPurpose.FurnitureOrEquipment,
        SavingsAccount = SavingsAccount.UnknownOrNone,
        EmploymentDuration = EmploymentDuration.From1To4Years,
        PersonalStatusSex = PersonalStatusSex.MaleMarriedOrWidowed,
        OtherDebtors = OtherDebtors.None,
        Property = PropertyType.BuildingSocietySavingsOrLifeInsurance,
        OtherInstallmentPlans = OtherInstallmentPlans.None,
        Housing = Housing.Rent,
        Job = JobType.SkilledEmployeeOrOfficial,
        HasRegisteredTelephone = false,
        IsForeignWorker = false,
    };

    public static readonly MlPredictRequest ContractRequest = new()
    {
        CheckingAccountStatus = "A12",
        DurationMonths = 24,
        CreditHistory = "A32",
        Purpose = "A43",
        CreditAmount = 3500,
        SavingsAccount = "A61",
        EmploymentDuration = "A73",
        InstallmentRate = 3,
        PersonalStatusSex = "A93",
        OtherDebtorsGuarantors = "A101",
        PresentResidenceSince = 2,
        Property = "A123",
        Age = 35,
        OtherInstallmentPlans = "A143",
        Housing = "A152",
        ExistingCreditsCount = 1,
        Job = "A173",
        NumPeopleLiable = 1,
        Telephone = "A191",
        ForeignWorker = "A201",
    };

    public static CreditApplication ContractApplication() => new()
    {
        ApplicantName = "Test Applicant",
        ClientId = Client.Id,
        Client = Client,
        SubmittedBy = "tester",
        SubmittedAt = Now,
        DurationMonths = 24,
        CreditAmount = 3500,
        Age = 35,
        InstallmentSharePercent = 22m,
        YearsAtResidence = 2,
        CreditsAtBank = 1,
        Dependants = 0,
        CheckingAccountStatus = CheckingAccountStatus.Negative,
        CreditHistory = CreditHistory.NoCreditsOrAllPaidDuly,
        Purpose = LoanPurpose.FurnitureOrEquipment,
        SavingsAccount = SavingsAccount.UnknownOrNone,
        EmploymentDuration = EmploymentDuration.From1To4Years,
        PersonalStatusSex = PersonalStatusSex.MaleMarriedOrWidowed,
        OtherDebtors = OtherDebtors.None,
        Property = PropertyType.BuildingSocietySavingsOrLifeInsurance,
        OtherInstallmentPlans = OtherInstallmentPlans.None,
        Housing = Housing.Rent,
        Job = JobType.SkilledEmployeeOrOfficial,
        HasRegisteredTelephone = false,
        IsForeignWorker = false,
    };
}
