using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Decisions;

public interface ICreditDecisionService
{
    Task<CreditDecision> EvaluateAsync(CreditApplication application, CancellationToken cancellationToken);
}

public static class DecisionErrorCodes
{
    public const string ModelMismatch = "model_mismatch";
}
