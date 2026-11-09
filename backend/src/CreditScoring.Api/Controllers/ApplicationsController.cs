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
    [Authorize(Policy = Permissions.ApplicationsWrite)]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApplicationResponse>> Create(
        CreateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (CreditLineProblem(request.EarliestCreditLine!.Value, now) is { } line)
            return FieldErrors.Reject(this, "earliestCreditLine", line.Code, line.Message);

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken);
        if (client is null)
            return FieldErrors.Reject(this, "clientId", new FieldError("client_not_found"), "No client with this id exists.");
        if (!client.IsActive)
            return FieldErrors.Reject(this, "clientId", new FieldError("client_inactive"), "This client is inactive and cannot get new applications.");

        var application = request.ToDomain(now, Credentials.ActorOf(User), client);
        var models = await db.ScoringModels.AsNoTracking().ToListAsync(cancellationToken);
        var evaluation = await decisions.EvaluateAsync(application, models, cancellationToken);
        application.Decision = evaluation.Decision;
        application.ShadowScores.AddRange(evaluation.ShadowScores);

        db.Applications.Add(application);
        await db.SaveChangesAsync(CancellationToken.None);

        return CreatedAtAction(nameof(Get), new { id = application.Id }, ApplicationResponse.From(application));
    }

    private static (FieldError Code, string Message)? CreditLineProblem(DateOnly earliestCreditLine, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var line = new DateOnly(earliestCreditLine.Year, earliestCreditLine.Month, 1);
        if (line > thisMonth)
            return (new FieldError("after_this_month"), "The first credit line cannot be opened after this month.");
        if (line < thisMonth.AddYears(-BusinessLimits.MaxCreditHistoryYears))
            return (
                new FieldError("too_long_ago", new Dictionary<string, object> { ["maximumYears"] = BusinessLimits.MaxCreditHistoryYears }),
                $"The first credit line cannot be more than {BusinessLimits.MaxCreditHistoryYears} years ago.");
        return null;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.ApplicationsRead)]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var application = await WithDetails(db.Applications.AsNoTracking())
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return application is null ? NotFound() : ApplicationResponse.From(application);
    }

    [HttpPost("{id:guid}/rescore")]
    [Authorize(Policy = Permissions.ApplicationsWrite)]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationResponse>> Rescore(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await WithDetails(db.Applications.AsNoTracking())
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (snapshot is null)
            return NotFound();

        var models = await db.ScoringModels.AsNoTracking().ToListAsync(cancellationToken);
        switch (await decisions.RescoreAsync(snapshot, models, cancellationToken))
        {
            case RescoreRefused refused:
                return RescoreConflict(refused.Message);

            case Rescored rescored:
                await using (var transaction = await db.Database.BeginTransactionAsync(CancellationToken.None))
                {
                    await LockAsync(id);
                    var application = await WithDetails(db.Applications)
                        .Include(a => a.ShadowScores)
                        .SingleAsync(a => a.Id == id, CancellationToken.None);
                    if (Rescoring.Refusal(application) is { } refusal)
                        return RescoreConflict(refusal);
                    if (application.Decision?.Id != snapshot.Decision?.Id)
                        return RescoreConflict("This application was scored again by someone else.");

                    if (rescored.Previous is { } previous)
                        application.ScoringFailures.Add(previous);
                    if (application.Decision is { } failed)
                        db.Decisions.Remove(failed);
                    application.Decision = rescored.Evaluation.Decision;
                    db.ShadowScores.RemoveRange(application.ShadowScores);
                    application.ShadowScores.Clear();
                    application.ShadowScores.AddRange(rescored.Evaluation.ShadowScores);

                    await db.SaveChangesAsync(CancellationToken.None);
                    await transaction.CommitAsync(CancellationToken.None);
                    return Ok(ApplicationResponse.From(application));
                }

            default:
                throw new UnreachableException();
        }
    }

    private static IQueryable<CreditApplication> WithDetails(IQueryable<CreditApplication> applications) =>
        applications
            .Include(a => a.Client)
            .Include(a => a.Decision!).ThenInclude(d => d.Factors)
            .Include(a => a.OfficerDecision)
            .Include(a => a.ScoringFailures);

    private Task LockAsync(Guid id) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM credit_applications WHERE id = {id} FOR UPDATE",
            CancellationToken.None);

    private ConflictObjectResult RescoreConflict(string message) => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = message,
    });

    [HttpGet("{id:guid}/shadow-scores")]
    [Authorize(Policy = Permissions.StatsRead)]
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
    [Authorize(Policy = Permissions.ApplicationsDecide)]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationResponse>> Decide(
        Guid id,
        RecordDecisionRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(id);
        var application = await WithDetails(db.Applications)
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
                    await transaction.CommitAsync(CancellationToken.None);
                }
                catch (DbUpdateException ex) when (Pg.IsUniqueViolation(ex))
                {
                    return AlreadyDecidedConflict();
                }
                return CreatedAtAction(nameof(Get), new { id }, ApplicationResponse.From(application));

            case DecisionRefused refused:
                return FieldErrors.Reject(this, refused.Field, new FieldError(refused.Code), refused.Message);

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
    [Authorize(Policy = Permissions.ApplicationsRead)]
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
