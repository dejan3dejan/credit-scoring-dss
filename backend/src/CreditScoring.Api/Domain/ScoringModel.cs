namespace CreditScoring.Api.Domain;

public enum ModelRole
{
    Active = 1,
    Shadow = 2,
    Retired = 3,
}

public class ScoringModel
{
    private ScoringModel() { }

    public string ModelId { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string Dataset { get; private set; } = "";
    public double Threshold { get; private set; }
    public double? ReviewFrom { get; private set; }
    public ModelRole Role { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public Guid? RegisteredByUserId { get; private set; }

    internal static ScoringModel Create(
        string modelId,
        string displayName,
        string dataset,
        double threshold,
        double? reviewFrom,
        Actor registeredBy,
        DateTimeOffset registeredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        if (!double.IsFinite(threshold) || threshold <= 0 || threshold >= 1)
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "must be in (0, 1)");
        if (reviewFrom is { } from && !(from > 0 && from < threshold))
            throw new ArgumentOutOfRangeException(nameof(reviewFrom), from, "must be in (0, threshold)");

        return new ScoringModel
        {
            ModelId = modelId.Trim(),
            DisplayName = displayName.Trim(),
            Dataset = dataset.Trim(),
            Threshold = threshold,
            ReviewFrom = reviewFrom,
            Role = ModelRole.Shadow,
            RegisteredAt = registeredAt,
            RegisteredByUserId = registeredBy.UserId,
        };
    }

    public Recommendation RecommendationFor(double pBad) =>
        pBad >= Threshold ? Recommendation.Reject
        : ReviewFrom is { } from && pBad >= from ? Recommendation.Review
        : Recommendation.Approve;

    internal ModelEvent MoveTo(ModelRole role, string reason, Actor changedBy, DateTimeOffset changedAt)
    {
        var from = Role;
        Role = role;
        return ModelEvent.Create(ModelId, from, role, reason, changedBy, changedAt);
    }
}

public class ModelEvent
{
    private ModelEvent() { }

    public Guid Id { get; private set; }
    public string ModelId { get; private set; } = "";
    public ModelRole? FromRole { get; private set; }
    public ModelRole ToRole { get; private set; }
    public string? Reason { get; private set; }
    public string ChangedBy { get; private set; } = "";
    public Guid? ChangedByUserId { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    internal static ModelEvent Create(
        string modelId,
        ModelRole? from,
        ModelRole to,
        string? reason,
        Actor changedBy,
        DateTimeOffset changedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(changedBy.Name);
        if (changedBy.UserId == Guid.Empty)
            throw new ArgumentException("a registry change needs a signed-in user", nameof(changedBy));

        return new ModelEvent
        {
            Id = Guid.CreateVersion7(changedAt),
            ModelId = modelId,
            FromRole = from,
            ToRole = to,
            Reason = reason,
            ChangedBy = changedBy.Name.Trim(),
            ChangedByUserId = changedBy.UserId,
            ChangedAt = changedAt,
        };
    }
}

public abstract record RegistryResult;

public sealed record RegistryChanged(IReadOnlyList<ModelEvent> Events) : RegistryResult;

public sealed record RegistryRefused(string Field, string Code, string Message) : RegistryResult;

public sealed record ModelNotRegistered : RegistryResult;

public static class ModelRegistry
{
    public static (ScoringModel Model, ModelEvent Event) Register(
        string modelId,
        string displayName,
        string dataset,
        double threshold,
        double? reviewFrom,
        Actor registeredBy,
        DateTimeOffset now)
    {
        var model = ScoringModel.Create(modelId, displayName, dataset, threshold, reviewFrom, registeredBy, now);
        return (model, ModelEvent.Create(model.ModelId, null, model.Role, null, registeredBy, now));
    }

    public static RegistryResult Promote(
        IReadOnlyCollection<ScoringModel> models,
        string modelId,
        string? reason,
        Actor changedBy,
        DateTimeOffset now)
    {
        if (models.FirstOrDefault(m => m.ModelId == modelId) is not { } target)
            return new ModelNotRegistered();
        if (target.Role == ModelRole.Active)
            return new RegistryRefused("modelId", "model_already_active", "This model is already active.");
        if (target.Role == ModelRole.Retired)
            return new RegistryRefused("modelId", "model_retired", "A retired model cannot be made active.");
        if (string.IsNullOrWhiteSpace(reason))
            return new RegistryRefused("reason", "reason_required", "Give a reason for making this model active.");

        var why = reason.Trim();
        var events = models
            .Where(m => m.Role == ModelRole.Active)
            .Select(m => m.MoveTo(ModelRole.Shadow, why, changedBy, now))
            .ToList();
        events.Add(target.MoveTo(ModelRole.Active, why, changedBy, now));
        return new RegistryChanged(events);
    }

    public static RegistryResult Retire(
        IReadOnlyCollection<ScoringModel> models,
        string modelId,
        string? reason,
        Actor changedBy,
        DateTimeOffset now)
    {
        if (models.FirstOrDefault(m => m.ModelId == modelId) is not { } target)
            return new ModelNotRegistered();
        if (target.Role == ModelRole.Active)
            return new RegistryRefused("modelId", "model_is_active", "Make another model active before retiring this one.");
        if (target.Role == ModelRole.Retired)
            return new RegistryRefused("modelId", "model_already_retired", "This model is already retired.");
        if (string.IsNullOrWhiteSpace(reason))
            return new RegistryRefused("reason", "reason_required", "Give a reason for retiring this model.");

        return new RegistryChanged([target.MoveTo(ModelRole.Retired, reason.Trim(), changedBy, now)]);
    }
}
