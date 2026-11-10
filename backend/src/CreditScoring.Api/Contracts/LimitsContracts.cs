namespace CreditScoring.Api.Contracts;

public sealed record LimitRange(double Minimum, double Maximum);

public sealed record ApplicationLimits(
    IReadOnlyDictionary<string, LimitRange> Ranges,
    int TermMonths,
    int CreditLineMaximumYears,
    int CreditLineMinimumAge);

public sealed record ClientLimits(int MinimumAge, int MaximumAge);

public sealed record LimitsResponse(ApplicationLimits Application, ClientLimits Client);

public static class Limits
{
    public static LimitsResponse Current() => new(
        new ApplicationLimits(
            new Dictionary<string, LimitRange>
            {
                ["loanAmount"] = new(BusinessLimits.MinLoanAmount, BusinessLimits.MaxLoanAmount),
                ["annualIncome"] = new(0, BusinessLimits.MaxAnnualIncome),
                ["debtToIncomePercent"] = new(0, BusinessLimits.MaxDebtToIncomePercent),
                ["creditScore"] = new(BusinessLimits.MinCreditScore, BusinessLimits.MaxCreditScore),
                ["inquiriesLast6Months"] = new(0, BusinessLimits.MaxRecentCount),
                ["delinquenciesLast2Years"] = new(0, BusinessLimits.MaxRecentCount),
                ["publicRecords"] = new(0, BusinessLimits.MaxRecentCount),
                ["bankruptcies"] = new(0, BusinessLimits.MaxRecentCount),
                ["revolvingBalance"] = new(0, BusinessLimits.MaxRevolvingBalance),
                ["revolvingUtilizationPercent"] = new(0, BusinessLimits.MaxRevolvingUtilizationPercent),
                ["openAccounts"] = new(0, BusinessLimits.MaxOpenAccounts),
                ["totalAccounts"] = new(0, BusinessLimits.MaxTotalAccounts),
                ["mortgageAccounts"] = new(0, BusinessLimits.MaxRecentCount),
            },
            BusinessLimits.TermMonths,
            BusinessLimits.MaxCreditHistoryYears,
            BusinessLimits.MinClientAge),
        new ClientLimits(BusinessLimits.MinClientAge, BusinessLimits.MaxClientAge));
}
