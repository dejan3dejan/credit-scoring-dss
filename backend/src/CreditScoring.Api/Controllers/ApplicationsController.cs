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
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken);
        if (client is null)
            return FieldErrors.Reject(this, "clientId", new FieldError("client_not_found"), "No client with this id exists.");
        if (!client.IsActive)
            return FieldErrors.Reject(this, "clientId", new FieldError("client_inactive"), "This client is inactive and cannot get new applications.");

        var actor = Credentials.ActorOf(User);
        ApplicationDraft? draft = null;
        if (request.DraftId is { } draftId)
        {
            draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId && d.CreatedByUserId == actor.UserId, cancellationToken);
            if (draft is null)
                return FieldErrors.Reject(this, "draftId", new FieldError("draft_not_found"), "No draft of yours with this id exists.");
            if (draft.Status != DraftStatus.Open)
                return FieldErrors.Reject(this, "draftId", new FieldError("draft_closed"), "This draft was already submitted or discarded.");
        }

        if (await OpenApplicationOf(client.Id, cancellationToken) is { } open)
            return OpenApplicationConflict(open);
        if (ApplicationChecks.Business(request, client, now) is { Count: > 0 } problems)
            return FieldErrors.RejectAll(this, problems);

        var application = request.ToDomain(now, actor, client);
        var submitter = await db.Users.SingleAsync(u => u.Id == actor.UserId, cancellationToken);
        if (application.AssignTo(submitter) is not Changed)
            throw new InvalidOperationException("a new application could not be assigned to its submitter");
        var models = await db.ScoringModels.AsNoTracking().ToListAsync(cancellationToken);
        var evaluation = await decisions.EvaluateAsync(application, models, cancellationToken);
        application.Decision = evaluation.Decision;
        application.ShadowScores.AddRange(evaluation.ShadowScores);

        db.Applications.Add(application);
        if (draft is not null && draft.MarkSubmitted(application.Id, now) is not Changed)
            throw new InvalidOperationException("an open draft could not be marked as submitted");
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateException ex) when (Pg.IsUniqueViolation(ex, CreditScoringDbContext.OneOpenApplicationIndex))
        {
            var winner = await OpenApplicationOf(client.Id, CancellationToken.None)
                ?? throw new InvalidOperationException("the open-application index refused a save but no open application exists", ex);
            return OpenApplicationConflict(winner);
        }

        return CreatedAtAction(nameof(Get), new { id = application.Id }, ApplicationResponse.From(application));
    }

    private Task<OpenApplication?> OpenApplicationOf(Guid clientId, CancellationToken cancellationToken) =>
        OpenApplications(db).Where(a => a.ClientId == clientId)
            .Select(a => new OpenApplication(a.Id, a.Reference))
            .FirstOrDefaultAsync(cancellationToken);

    internal static IQueryable<CreditApplication> OpenApplications(CreditScoringDbContext db) =>
        db.Applications.AsNoTracking().Where(a => a.Stage == ApplicationStage.Submitted || a.Stage == ApplicationStage.AwaitingSignOff);

    private ObjectResult OpenApplicationConflict(OpenApplication open) =>
        ApiProblems.Create(this, StatusCodes.Status409Conflict, ApplicationChecks.ClientHasOpenApplication,
            $"The client already has an open application ({open.Reference}). Decide or withdraw it first.",
            new Dictionary<string, object?> { ["applicationId"] = open.Id, ["reference"] = open.Reference });

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
            .Include(a => a.ScoringFailures)
            .Include(a => a.AssignedTo)
            .Include(a => a.SignOffs)
            .Include(a => a.LoanOutcome);

    private Task LockAsync(Guid id) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM credit_applications WHERE id = {id} FOR UPDATE",
            CancellationToken.None);

    private ObjectResult RescoreConflict(string message) =>
        ApiProblems.Create(this, StatusCodes.Status409Conflict, ApiProblems.Conflict, message);

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

        var actsForBranch = Credentials.Has(User, Permissions.ApplicationsApproveEscalated);
        switch (decisions.RecordOfficerDecision(application, request.Outcome!.Value, request.Reason, Credentials.ActorOf(User), actsForBranch))
        {
            case DecisionRecorded or SignOffRequested:
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

            case NotAssignee:
                return NotAssigneeProblem();

            case NotDecidable:
                return ApiProblems.Create(this, StatusCodes.Status409Conflict, "not_decidable",
                    "Only a submitted application can be decided.");

            default:
                throw new UnreachableException();
        }
    }

    [HttpPost("{id:guid}/sign-off")]
    [Authorize(Policy = Permissions.ApplicationsApproveEscalated)]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApplicationResponse>> SignOff(Guid id, SignOffReviewRequest request, CancellationToken cancellationToken) =>
        ChangeLockedAsync(id, application =>
            decisions.ReviewSignOff(application, request.Approve!.Value, request.Note, Credentials.ActorOf(User)), cancellationToken);

    [HttpPost("{id:guid}/outcome")]
    [Authorize(Policy = Permissions.OutcomesRecord)]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApplicationResponse>> RecordOutcome(Guid id, RecordOutcomeRequest request, CancellationToken cancellationToken) =>
        ChangeLockedAsync(id, application =>
            application.RecordOutcome(request.Outcome!.Value, request.Note, Credentials.ActorOf(User), time.GetUtcNow()), cancellationToken);

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = Permissions.ApplicationsAssign)]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationResponse>> Assign(Guid id, AssignRequest request, CancellationToken cancellationToken)
    {
        var branchId = await db.Applications.Where(a => a.Id == id).Select(a => (Guid?)a.BranchId).FirstOrDefaultAsync(cancellationToken);
        if (branchId is null)
            return NotFound();

        var decisionRoles = Permissions.RolesWith(Permissions.ApplicationsDecide);
        var assignee = await db.Users.FirstOrDefaultAsync(
            u => u.Id == request.UserId && u.IsActive && u.BranchId == branchId && decisionRoles.Contains(u.Role),
            cancellationToken);
        if (assignee is null)
            return FieldErrors.Reject(this, "userId", new FieldError("assignee_not_found"),
                "Choose an active officer or manager of the application's branch.");

        return await ChangeLockedAsync(id, application => application.AssignTo(assignee), cancellationToken);
    }

    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Policy = Permissions.ApplicationsWrite)]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApplicationResponse>> Withdraw(Guid id, WithdrawRequest request, CancellationToken cancellationToken)
    {
        var actor = Credentials.ActorOf(User);
        var actsForBranch = Credentials.Has(User, Permissions.ApplicationsAssign);
        return ChangeLockedAsync(id, application =>
            application.MayBeHandledBy(actor.UserId, actsForBranch)
                ? application.Withdraw(request.Reason!, actor, time.GetUtcNow())
                : new Refused(null, NotAssigneeCode, "Only the assigned officer or a manager can withdraw this application."),
            cancellationToken);
    }

    [HttpGet("{id:guid}/notes")]
    [Authorize(Policy = Permissions.ApplicationsRead)]
    [ProducesResponseType<IReadOnlyList<NoteResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<NoteResponse>>> Notes(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.Applications.AnyAsync(a => a.Id == id, cancellationToken))
            return NotFound();

        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.CreditApplicationId == id)
            .OrderBy(n => n.CreatedAt)
            .ThenBy(n => n.Id)
            .ToListAsync(cancellationToken);
        return notes.Select(NoteResponse.From).ToList();
    }

    [HttpPost("{id:guid}/notes")]
    [Authorize(Policy = Permissions.ApplicationsWrite)]
    [ProducesResponseType<NoteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NoteResponse>> AddNote(Guid id, NoteRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Applications.AnyAsync(a => a.Id == id, cancellationToken))
            return NotFound();

        var note = ApplicationNote.Write(id, request.Text!, Credentials.ActorOf(User), time.GetUtcNow());
        db.Notes.Add(note);
        await db.SaveChangesAsync(CancellationToken.None);
        return CreatedAtAction(nameof(Notes), new { id }, NoteResponse.From(note));
    }

    private const string NotAssigneeCode = "not_assignee";

    private ObjectResult NotAssigneeProblem() =>
        ApiProblems.Create(this, StatusCodes.Status403Forbidden, NotAssigneeCode,
            "Only the assigned officer or a manager can act on this application.");

    private async Task<ActionResult<ApplicationResponse>> ChangeLockedAsync(
        Guid id,
        Func<CreditApplication, ChangeResult> change,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(id);
        var application = await WithDetails(db.Applications).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application is null)
            return NotFound();

        switch (change(application))
        {
            case Changed:
                await db.SaveChangesAsync(CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);
                return ApplicationResponse.From(application);

            case Refused { Field: { } field } refused:
                return FieldErrors.Reject(this, field, new FieldError(refused.Code), refused.Message);

            case Refused { Code: NotAssigneeCode }:
                return NotAssigneeProblem();

            case Refused refused:
                return ApiProblems.Create(this, StatusCodes.Status409Conflict, refused.Code, refused.Message);

            default:
                throw new UnreachableException();
        }
    }

    private ObjectResult AlreadyDecidedConflict() =>
        ApiProblems.Create(this, StatusCodes.Status409Conflict, ApiProblems.Conflict, "A decision is already recorded for this application.");

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

        if (query.Stage is { } stage)
        {
            applications = applications.Where(a => a.Stage == stage);
        }

        applications = query.LoanOutcome switch
        {
            LoanOutcomeFilter.Pending => applications.Where(a => a.Stage == ApplicationStage.Decided
                && a.OfficerDecision!.Outcome == FinalDecision.Approve
                && a.LoanOutcome == null),
            LoanOutcomeFilter.Repaid => applications.Where(a => a.LoanOutcome!.Outcome == LoanOutcomeKind.Repaid),
            LoanOutcomeFilter.Defaulted => applications.Where(a => a.LoanOutcome!.Outcome == LoanOutcomeKind.Defaulted),
            _ => applications,
        };

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
