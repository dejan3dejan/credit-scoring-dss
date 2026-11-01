namespace CreditScoring.Api.Contracts;

public static class BusinessLimits
{
    public const int MinDurationMonths = 4;
    public const int MaxDurationMonths = 60;

    public const int MinCreditAmount = 250;
    public const int MaxCreditAmount = 16_000;

    public const int MinAge = 18;
    public const int MaxAge = 75;

    public const double MinInstallmentSharePercent = 0;
    public const double MaxInstallmentSharePercent = 100;

    public const int MinCreditsAtBank = 1;
    public const int MaxCreditsAtBank = 20;

    public const int MaxDependants = 20;

    public const int ApplicantNameMaxLength = 200;
    public const int SubmittedByMaxLength = 100;
    public const int DecisionReasonMaxLength = 500;
}
