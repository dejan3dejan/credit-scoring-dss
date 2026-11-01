using System.Diagnostics;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/applications")]
public class ApplicationsController(
    CreditScoringDbContext db,
    ICreditDecisionService decisions,
    TimeProvider time) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.CanDecide)]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApplicationResponse>> Create(
        CreateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken);
        if (client is null)
        {
            ModelState.AddModelError("clientId", "No client with this id exists.");
            return BadRequest(new ValidationProblemDetails(ModelState) { Status = StatusCodes.Status400BadRequest });
        }

        var application = request.ToDomain(time.GetUtcNow(), Credentials.ActorOf(User), client);
        var models = await db.ScoringModels.AsNoTracking().ToListAsync(cancellationToken);
        var evaluation = await decisions.EvaluateAsync(application, models, cancellationToken);
        application.Decision = evaluation.Decision;
        application.ShadowScores.AddRange(evaluation.ShadowScores);

        db.Applications.Add(application);
        await db.SaveChangesAsync(CancellationToken.None);

        return CreatedAtAction(nameof(Get), new { id = application.Id }, ApplicationResponse.From(application));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var application = await db.Applications
            .AsNoTracking()
            .Include(a => a.Client)
            .Include(a => a.Decision)
            .Include(a => a.OfficerDecision)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return application is null ? NotFound() : ApplicationResponse.From(application);
    }

    [HttpGet("{id:guid}/shadow-scores")]
    [Authorize(Policy = Policies.CanSeeStats)]
    [ProducesResponseType<IReadOnlyList<ShadowScoreResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ShadowScoreResponse>>> ShadowScores(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.Applications.AnyAsync(a => a.Id == id, cancellationToken))
            return NotFound();

        var scores = await db.ShadowScores
            .AsNoTracking()
            .Where(s => s.CreditApplicationId == id)
            .OrderBy(s => s.ModelId)
            .ToListAsync(cancellationToken);
        return Ok(scores.Select(ShadowScoreResponse.From).ToList());
    }

    [HttpPost("{id:guid}/decision")]
    [Authorize(Policy = Policies.CanDecide)]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationResponse>> Decide(
        Guid id,
        RecordDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var application = await db.Applications
            .Include(a => a.Client)
            .Include(a => a.Decision)
            .Include(a => a.OfficerDecision)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application is null)
            return NotFound();

        switch (decisions.RecordOfficerDecision(application, request.Outcome!.Value, request.Reason, Credentials.ActorOf(User)))
        {
            case DecisionRecorded recorded:
                application.OfficerDecision = recorded.Decision;
                try
                {
                    await db.SaveChangesAsync(CancellationToken.None);
                }
                catch (DbUpdateException ex) when (Pg.IsUniqueViolation(ex))
                {
                    return AlreadyDecidedConflict();
                }
                return CreatedAtAction(nameof(Get), new { id }, ApplicationResponse.From(application));

            case DecisionRefused refused:
                ModelState.AddModelError(refused.Field, refused.Message);
                return BadRequest(new ValidationProblemDetails(ModelState) { Status = StatusCodes.Status400BadRequest });

            case AlreadyDecided:
                return AlreadyDecidedConflict();

            default:
                throw new UnreachableException();
        }
    }

    private ConflictObjectResult AlreadyDecidedConflict() => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "A decision is already recorded for this application.",
    });

    [HttpGet]
    [ProducesResponseType<PagedResult<ApplicationSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ApplicationSummary>>> List(
        [FromQuery] HistoryQuery query,
        CancellationToken cancellationToken)
    {
        var applications = db.Applications.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.ApplicantName))
        {
            var pattern = Like.Contains(query.ApplicantName);
            applications = applications.Where(a => EF.Functions.ILike(a.ApplicantName, pattern, Like.Escape));
        }
        if (query.From is { } from)
        {
            var fromUtc = from.ToUniversalTime();
            applications = applications.Where(a => a.SubmittedAt >= fromUtc);
        }
        if (query.To is { } to)
        {
            var toUtc = to.ToUniversalTime();
            applications = applications.Where(a => a.SubmittedAt < toUtc);
        }
        if (query.Status is { } status)
        {
            applications = applications.Where(a => a.Decision!.Status == status);
        }
        if (query.Recommendation is { } recommendation)
        {
            applications = applications.Where(a => a.Decision!.Recommendation == recommendation);
        }

        applications = query.Review switch
        {
            ReviewFilter.Pending => applications.Where(a => a.OfficerDecision == null),
            ReviewFilter.Approved => applications.Where(a => a.OfficerDecision!.Outcome == FinalDecision.Approve),
            ReviewFilter.Rejected => applications.Where(a => a.OfficerDecision!.Outcome == FinalDecision.Reject),
            ReviewFilter.Override => applications.Where(a => a.OfficerDecision!.IsOverride),
            _ => applications,
        };

        var total = await applications.CountAsync(cancellationToken);
        var items = await applications
            .OrderByDescending(a => a.SubmittedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ApplicationSummary.FromApplication)
            .ToListAsync(cancellationToken);

        return new PagedResult<ApplicationSummary>(items, query.Page, query.PageSize, total);
    }
}
