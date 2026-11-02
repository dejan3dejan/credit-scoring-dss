using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;

namespace CreditScoring.Api.Decisions;

public static class LendingClubMapper
{
    private const double DaysPerYear = 365.25;

    public static MlPredictRequest ToMlRequest(CreditApplication application) => new()
    {
        LoanAmnt = application.LoanAmount,
        Purpose = ToCode(application.Purpose),
        EmpLengthYears = EmploymentYears(application.EmploymentLength),
        HomeOwnership = ToCode(application.HomeOwnership),
        AnnualInc = (double)application.AnnualIncome,
        VerificationStatus = ToCode(application.IncomeVerification),
        Dti = (double)application.DebtToIncomePercent,
        Fico = application.CreditScore,
        CreditHistoryYears = CreditHistoryYears(application.EarliestCreditLine, application.SubmittedAt),
        InqLast6mths = application.InquiriesLast6Months,
        Delinq2yrs = application.DelinquenciesLast2Years,
        PubRec = application.PublicRecords,
        PubRecBankruptcies = application.Bankruptcies,
        RevolBal = (double)application.RevolvingBalance,
        RevolUtil = (double)application.RevolvingUtilizationPercent,
        OpenAcc = application.OpenAccounts,
        TotalAcc = application.TotalAccounts,
        MortAcc = application.MortgageAccounts,
    };

    public static double CreditHistoryYears(DateOnly earliestCreditLine, DateTimeOffset submittedAt)
    {
        var submitted = DateOnly.FromDateTime(submittedAt.UtcDateTime);
        var from = new DateOnly(earliestCreditLine.Year, earliestCreditLine.Month, 1);
        var to = new DateOnly(submitted.Year, submitted.Month, 1);
        if (from > to)
            throw new ArgumentOutOfRangeException(nameof(earliestCreditLine), earliestCreditLine, "is after the submission month");
        return (to.DayNumber - from.DayNumber) / DaysPerYear;
    }

    public static int? EmploymentYears(EmploymentLength value) => value switch
    {
        EmploymentLength.NotStated => null,
        EmploymentLength.LessThanOneYear => 0,
        EmploymentLength.OneYear => 1,
        EmploymentLength.TwoYears => 2,
        EmploymentLength.ThreeYears => 3,
        EmploymentLength.FourYears => 4,
        EmploymentLength.FiveYears => 5,
        EmploymentLength.SixYears => 6,
        EmploymentLength.SevenYears => 7,
        EmploymentLength.EightYears => 8,
        EmploymentLength.NineYears => 9,
        EmploymentLength.TenOrMoreYears => 10,
        _ => throw Undefined(value),
    };

    public static string ToCode(LoanPurpose value) => value switch
    {
        LoanPurpose.DebtConsolidation => "debt_consolidation",
        LoanPurpose.CreditCard => "credit_card",
        LoanPurpose.HomeImprovement => "home_improvement",
        LoanPurpose.MajorPurchase => "major_purchase",
        LoanPurpose.Car => "car",
        LoanPurpose.Medical => "medical",
        LoanPurpose.Moving => "moving",
        LoanPurpose.House => "house",
        LoanPurpose.Vacation => "vacation",
        LoanPurpose.Wedding => "wedding",
        LoanPurpose.SmallBusiness => "small_business",
        LoanPurpose.RenewableEnergy => "renewable_energy",
        LoanPurpose.Other => "other",
        _ => throw Undefined(value),
    };

    public static string ToCode(HomeOwnership value) => value switch
    {
        HomeOwnership.Rent => "RENT",
        HomeOwnership.Own => "OWN",
        HomeOwnership.Mortgage => "MORTGAGE",
        HomeOwnership.Other => "OTHER",
        _ => throw Undefined(value),
    };

    public static string ToCode(IncomeVerification value) => value switch
    {
        IncomeVerification.NotVerified => "Not Verified",
        IncomeVerification.SourceVerified => "Source Verified",
        IncomeVerification.Verified => "Verified",
        _ => throw Undefined(value),
    };

    private static ArgumentOutOfRangeException Undefined<T>(T value) where T : struct, Enum =>
        new(typeof(T).Name, value, $"undefined {typeof(T).Name} value");
}
