namespace CreditScoring.Api.Contracts;

public static class BusinessLimits
{
    public const int TermMonths = 36;

    public const int MinLoanAmount = 1_000;
    public const int MaxLoanAmount = 35_000;

    public const double MaxAnnualIncome = 10_000_000;
    public const double MaxRevolvingBalance = 10_000_000;
    public const double MaxDebtToIncomePercent = 100;
    public const double MaxRevolvingUtilizationPercent = 200;

    public const int MinCreditScore = 300;
    public const int MaxCreditScore = 850;

    public const int MaxCreditHistoryYears = 80;
    public const int MinClientAge = 18;
    public const int MaxClientAge = 100;
    public const int MaxRecentCount = 50;
    public const int MaxOpenAccounts = 100;
    public const int MaxTotalAccounts = 200;

    public const int ApplicantNameMaxLength = 200;
    public const int ClientNumberMaxLength = 16;
    public const int SubmittedByMaxLength = 100;
    public const int DecisionReasonMaxLength = 500;
    public const int UserNameMaxLength = 64;
    public const int PasswordMaxLength = 256;
    public const int PasswordMinLength = 10;
    public const int DisplayNameMaxLength = SubmittedByMaxLength;
    public const int EmailMaxLength = 254;
    public const int AddressMaxLength = 200;
    public const int PhoneMaxLength = 32;
    public const int EmployerMaxLength = 200;
    public const int CityMaxLength = 100;
    public const string PhonePattern = @"^[0-9+()/\s-]+$";
    public const string BranchCodePattern = @"^\s*[A-Za-z0-9-]{2,16}\s*$";
    public const string UserNamePattern = @"^\s*[A-Za-z0-9._-]+\s*$";
    public const int ModelIdMaxLength = 100;
}
