using System.Diagnostics;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/models")]
public class ModelsController(CreditScoringDbContext db, IMlClient ml, TimeProvider time) : ControllerBase
{
    private const int EventLimit = 50;

    [HttpGet]
    [Authorize(Policy = Permissions.ModelsRead)]
    [ProducesResponseType<IReadOnlyList<ScoringModelResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ScoringModelResponse>> List(CancellationToken cancellationToken)
    {
        var models = await db.ScoringModels.AsNoTracking().ToListAsync(cancellationToken);
        return models
            .OrderBy(m => m.Role)
            .ThenBy(m => m.ModelId, StringComparer.Ordinal)
            .Select(ScoringModelResponse.From)
            .ToList();
    }

    [HttpGet("available")]
    [Authorize(Policy = Permissions.ModelsManage)]
    [ProducesResponseType<IReadOnlyList<AvailableModelResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<AvailableModelResponse>>> Available(CancellationToken cancellationToken)
    {
        if (await ml.ListModelsAsync(cancellationToken) is not MlCatalog catalog)
            return CatalogUnavailable();

        var registered = await db.ScoringModels.Select(m => m.ModelId).ToListAsync(cancellationToken);
        return Ok(catalog.Models
            .Where(m => !registered.Contains(m.ModelId))
            .Select(AvailableModelResponse.From)
            .ToList());
    }

    [HttpPost]
    [Authorize(Policy = Permissions.ModelsManage)]
    [ProducesResponseType<ScoringModelResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ScoringModelResponse>> Register(RegisterModelRequest request, CancellationToken cancellationToken)
    {
        var modelId = request.ModelId!;
        if (await db.ScoringModels.AnyAsync(m => m.ModelId == modelId, cancellationToken))
            return Changed("This model is already registered.");
        if (await ml.ListModelsAsync(cancellationToken) is not MlCatalog catalog)
            return CatalogUnavailable();
        if (catalog.Models.FirstOrDefault(m => m.ModelId == modelId) is not { } info)
            return FieldErrors.Reject(this, "modelId", new FieldError("model_not_served"), "The inference service does not serve a model with this id.");

        var (model, registered) = ModelRegistry.Register(
            info.ModelId, info.ModelName, info.Dataset, info.RecommendedThreshold, info.ReviewFrom, Credentials.ActorOf(User), time.GetUtcNow());
        db.ScoringModels.Add(model);
        db.ModelEvents.Add(registered);
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateException ex) when (Pg.IsUniqueViolation(ex))
        {
            return Changed("This model is already registered.");
        }

        return CreatedAtAction(nameof(List), null, ScoringModelResponse.From(model));
    }

    [HttpPost("{modelId}/promote")]
    [Authorize(Policy = Permissions.ModelsManage)]
    [ProducesResponseType<IReadOnlyList<ScoringModelResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<IReadOnlyList<ScoringModelResponse>>> Promote(
        string modelId,
        ChangeModelRoleRequest request,
        CancellationToken cancellationToken) =>
        ChangeAsync(models => ModelRegistry.Promote(models, modelId, request.Reason, Credentials.ActorOf(User), time.GetUtcNow()), cancellationToken);

    [HttpPost("{modelId}/retire")]
    [Authorize(Policy = Permissions.ModelsManage)]
    [ProducesResponseType<IReadOnlyList<ScoringModelResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<IReadOnlyList<ScoringModelResponse>>> Retire(
        string modelId,
        ChangeModelRoleRequest request,
        CancellationToken cancellationToken) =>
        ChangeAsync(models => ModelRegistry.Retire(models, modelId, request.Reason, Credentials.ActorOf(User), time.GetUtcNow()), cancellationToken);

    [HttpGet("events")]
    [Authorize(Policy = Permissions.ModelsManage)]
    [ProducesResponseType<IReadOnlyList<ModelEventResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ModelEventResponse>> Events(CancellationToken cancellationToken)
    {
        var events = await db.ModelEvents
            .AsNoTracking()
            .OrderByDescending(e => e.ChangedAt)
            .ThenByDescending(e => e.Id)
            .Take(EventLimit)
            .ToListAsync(cancellationToken);
        return events.Select(ModelEventResponse.From).ToList();
    }

    private async Task<ActionResult<IReadOnlyList<ScoringModelResponse>>> ChangeAsync(
        Func<IReadOnlyCollection<ScoringModel>, RegistryResult> change,
        CancellationToken cancellationToken)
    {
        var models = await db.ScoringModels.ToListAsync(cancellationToken);
        switch (change(models))
        {
            case RegistryChanged changed:
                db.ModelEvents.AddRange(changed.Events);
                try
                {
                    await db.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception ex) when (Pg.IsExclusionViolation(ex))
                {
                    return Changed("Another change to the active model was saved first. Reload and try again.");
                }
                return Ok(await List(CancellationToken.None));

            case RegistryRefused refused:
                return FieldErrors.Reject(this, refused.Field, new FieldError(refused.Code), refused.Message);

            case ModelNotRegistered:
                return NotFound();

            default:
                throw new UnreachableException();
        }
    }

    private ObjectResult Changed(string title) =>
        ApiProblems.Create(this, StatusCodes.Status409Conflict, ApiProblems.Conflict, title);

    private ObjectResult CatalogUnavailable() =>
        ApiProblems.Create(this, StatusCodes.Status503ServiceUnavailable, ApiProblems.InferenceUnavailable,
            "The inference service could not list its models.");
}
