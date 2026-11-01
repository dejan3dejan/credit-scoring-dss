using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;

namespace CreditScoring.Api.Contracts;

public sealed record RegisterModelRequest
{
    [Required, StringLength(BusinessLimits.ModelIdMaxLength), Display(Name = "Model")]
    public string? ModelId { get; init; }
}

public sealed record ChangeModelRoleRequest
{
    [StringLength(BusinessLimits.DecisionReasonMaxLength), Display(Name = "Reason")]
    public string? Reason { get; init; }
}

public sealed record ScoringModelResponse(
    string ModelId,
    string DisplayName,
    string Dataset,
    double Threshold,
    ModelRole Role,
    DateTimeOffset RegisteredAt)
{
    public static ScoringModelResponse From(ScoringModel m) =>
        new(m.ModelId, m.DisplayName, m.Dataset, m.Threshold, m.Role, m.RegisteredAt);
}

public sealed record AvailableModelResponse(
    string ModelId,
    string ModelName,
    string Dataset,
    double RecommendedThreshold,
    DateTimeOffset CreatedAt)
{
    public static AvailableModelResponse From(MlModelInfo m) =>
        new(m.ModelId, m.ModelName, m.Dataset, m.RecommendedThreshold, m.CreatedAt);
}

public sealed record ModelEventResponse(
    Guid Id,
    string ModelId,
    ModelRole? FromRole,
    ModelRole ToRole,
    string? Reason,
    string ChangedBy,
    DateTimeOffset ChangedAt)
{
    public static ModelEventResponse From(ModelEvent e) =>
        new(e.Id, e.ModelId, e.FromRole, e.ToRole, e.Reason, e.ChangedBy, e.ChangedAt);
}

public sealed record ShadowScoreResponse(
    string ModelId,
    DecisionStatus Status,
    double? PBad,
    double? ThresholdUsed,
    Recommendation? Recommendation,
    string? ErrorCode,
    DateTimeOffset ScoredAt)
{
    public static ShadowScoreResponse From(ShadowScore s) =>
        new(s.ModelId, s.Status, s.PBad, s.ThresholdUsed, s.Recommendation, s.ErrorCode, s.ScoredAt);
}
