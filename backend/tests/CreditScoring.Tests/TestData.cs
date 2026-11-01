using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;

namespace CreditScoring.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

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
