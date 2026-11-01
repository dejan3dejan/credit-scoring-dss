namespace CreditScoring.Api.Ml;

public abstract record MlScoreResult;

public sealed record MlScore(double PBad, string ModelId) : MlScoreResult;

public sealed record MlFailure(
    string ErrorCode,
    int? HttpStatus,
    string Message,
    IReadOnlyList<MlErrorDetail> Details) : MlScoreResult;

public sealed record MlErrorDetail(string Field, string Issue);

public static class MlErrorCodes
{
    public const string Timeout = "timeout";
    public const string Unreachable = "unreachable";
    public const string UnexpectedStatus = "unexpected_status";
    public const string InvalidResponse = "invalid_response";
}
