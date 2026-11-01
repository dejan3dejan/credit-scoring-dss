using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;

namespace CreditScoring.Api.Decisions;

public static class StatlogMapper
{
    public static MlPredictRequest ToMlRequest(CreditApplication application) => new()
    {
        CheckingAccountStatus = ToCode(application.CheckingAccountStatus),
        DurationMonths = application.DurationMonths,
        CreditHistory = ToCode(application.CreditHistory),
        Purpose = ToCode(application.Purpose),
        CreditAmount = application.CreditAmount,
        SavingsAccount = ToCode(application.SavingsAccount),
        EmploymentDuration = ToCode(application.EmploymentDuration),
        InstallmentRate = InstallmentRateCode(application.InstallmentSharePercent),
        PersonalStatusSex = ToCode(application.PersonalStatusSex),
        OtherDebtorsGuarantors = ToCode(application.OtherDebtors),
        PresentResidenceSince = ResidenceCode(application.YearsAtResidence),
        Property = ToCode(application.Property),
        Age = application.Age,
        OtherInstallmentPlans = ToCode(application.OtherInstallmentPlans),
        Housing = ToCode(application.Housing),
        ExistingCreditsCount = ExistingCreditsCode(application.CreditsAtBank),
        Job = ToCode(application.Job),
        NumPeopleLiable = PeopleLiableCode(application.Dependants),
        Telephone = TelephoneCode(application.HasRegisteredTelephone),
        ForeignWorker = ForeignWorkerCode(application.IsForeignWorker),
    };

    public static int InstallmentRateCode(decimal sharePercent) => sharePercent switch
    {
        < 0 => throw new ArgumentOutOfRangeException(nameof(sharePercent), sharePercent, "must be >= 0"),
        >= 35 => 1,
        >= 25 => 2,
        >= 20 => 3,
        _ => 4,
    };

    public static int ResidenceCode(int years) => years switch
    {
        < 0 => throw new ArgumentOutOfRangeException(nameof(years), years, "must be >= 0"),
        < 1 => 1,
        < 4 => 2,
        < 7 => 3,
        _ => 4,
    };

    public static int ExistingCreditsCode(int creditsAtBank) => creditsAtBank switch
    {
        < 1 => throw new ArgumentOutOfRangeException(nameof(creditsAtBank), creditsAtBank, "must include this credit"),
        1 => 1,
        <= 3 => 2,
        <= 5 => 3,
        _ => 4,
    };

    public static int PeopleLiableCode(int dependants) => dependants switch
    {
        < 0 => throw new ArgumentOutOfRangeException(nameof(dependants), dependants, "must be >= 0"),
        <= 2 => 1,
        _ => 2,
    };

    public static string ToCode(CheckingAccountStatus value) => value switch
    {
        CheckingAccountStatus.NoAccount => "A11",
        CheckingAccountStatus.Negative => "A12",
        CheckingAccountStatus.ZeroTo200 => "A13",
        CheckingAccountStatus.AtLeast200OrSalaryAssignment => "A14",
        _ => throw Undefined(value),
    };

    public static string ToCode(CreditHistory value) => value switch
    {
        CreditHistory.PastPaymentDelays => "A30",
        CreditHistory.CriticalAccount => "A31",
        CreditHistory.NoCreditsOrAllPaidDuly => "A32",
        CreditHistory.ExistingPaidDulySoFar => "A33",
        CreditHistory.AllAtThisBankPaidDuly => "A34",
        _ => throw Undefined(value),
    };

    public static string ToCode(LoanPurpose value) => value switch
    {
        LoanPurpose.Other => "A40",
        LoanPurpose.NewCar => "A41",
        LoanPurpose.UsedCar => "A42",
        LoanPurpose.FurnitureOrEquipment => "A43",
        LoanPurpose.RadioOrTelevision => "A44",
        LoanPurpose.DomesticAppliances => "A45",
        LoanPurpose.Repairs => "A46",
        LoanPurpose.Vacation => "A48",
        LoanPurpose.Retraining => "A49",
        LoanPurpose.Business => "A410",
        _ => throw Undefined(value),
    };

    public static string ToCode(SavingsAccount value) => value switch
    {
        SavingsAccount.UnknownOrNone => "A61",
        SavingsAccount.Below100 => "A62",
        SavingsAccount.From100To500 => "A63",
        SavingsAccount.From500To1000 => "A64",
        SavingsAccount.AtLeast1000 => "A65",
        _ => throw Undefined(value),
    };

    public static string ToCode(EmploymentDuration value) => value switch
    {
        EmploymentDuration.Unemployed => "A71",
        EmploymentDuration.LessThan1Year => "A72",
        EmploymentDuration.From1To4Years => "A73",
        EmploymentDuration.From4To7Years => "A74",
        EmploymentDuration.AtLeast7Years => "A75",
        _ => throw Undefined(value),
    };

    public static string ToCode(PersonalStatusSex value) => value switch
    {
        PersonalStatusSex.MaleDivorcedOrSeparated => "A91",
        PersonalStatusSex.FemaleNonSingleOrMaleSingle => "A92",
        PersonalStatusSex.MaleMarriedOrWidowed => "A93",
        PersonalStatusSex.FemaleSingle => "A94",
        _ => throw Undefined(value),
    };

    public static string ToCode(OtherDebtors value) => value switch
    {
        OtherDebtors.None => "A101",
        OtherDebtors.CoApplicant => "A102",
        OtherDebtors.Guarantor => "A103",
        _ => throw Undefined(value),
    };

    public static string ToCode(PropertyType value) => value switch
    {
        PropertyType.UnknownOrNone => "A121",
        PropertyType.CarOrOther => "A122",
        PropertyType.BuildingSocietySavingsOrLifeInsurance => "A123",
        PropertyType.RealEstate => "A124",
        _ => throw Undefined(value),
    };

    public static string ToCode(OtherInstallmentPlans value) => value switch
    {
        OtherInstallmentPlans.Bank => "A141",
        OtherInstallmentPlans.Stores => "A142",
        OtherInstallmentPlans.None => "A143",
        _ => throw Undefined(value),
    };

    public static string ToCode(Housing value) => value switch
    {
        Housing.ForFree => "A151",
        Housing.Rent => "A152",
        Housing.Own => "A153",
        _ => throw Undefined(value),
    };

    public static string ToCode(JobType value) => value switch
    {
        JobType.UnemployedOrUnskilledNonResident => "A171",
        JobType.UnskilledResident => "A172",
        JobType.SkilledEmployeeOrOfficial => "A173",
        JobType.ManagerSelfEmployedOrHighlyQualified => "A174",
        _ => throw Undefined(value),
    };

    public static string TelephoneCode(bool hasRegisteredTelephone) => hasRegisteredTelephone ? "A192" : "A191";

    public static string ForeignWorkerCode(bool isForeignWorker) => isForeignWorker ? "A202" : "A201";

    private static ArgumentOutOfRangeException Undefined<T>(T value) where T : struct, Enum =>
        new(typeof(T).Name, value, $"undefined {typeof(T).Name} value");
}
