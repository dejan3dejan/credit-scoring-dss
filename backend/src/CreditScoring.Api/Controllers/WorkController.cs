using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/work")]
public class WorkController(CreditScoringDbContext db, TimeProvider time) : ControllerBase
{
    private const int ListLimit = 20;
    private static readonly TimeSpan RecentDecisions = TimeSpan.FromDays(7);

    [HttpGet("mine")]
    [Authorize(Policy = Permissions.ApplicationsWrite)]
    [ProducesResponseType<MyWorkResponse>(StatusCodes.Status200OK)]
    public async Task<MyWorkResponse> Mine(CancellationToken cancellationToken)
    {
        var me = Credentials.ActorOf(User).UserId;
        var since = time.GetUtcNow() - RecentDecisions;
        var applications = db.Applications.AsNoTracking();

        var drafts = db.Drafts.AsNoTracking().Include(d => d.Client)
            .Where(d => d.CreatedByUserId == me && d.Status == DraftStatus.Open);
        var assigned = applications.Where(a => a.AssignedToUserId == me && a.Stage == ApplicationStage.Submitted);
        var awaitingDecision = assigned.Where(a => a.Decision != null && a.Decision.Status == DecisionStatus.Scored);
        var scoringUnavailable = assigned.Where(a => a.Decision == null || a.Decision.Status == DecisionStatus.ScoringUnavailable);
        var awaitingSignOff = applications.Where(a => a.Stage == ApplicationStage.AwaitingSignOff
            && a.SignOffs.Any(s => s.Status == SignOffStatus.Pending && s.RequestedByUserId == me));
        var recentlyDecided = applications.Where(a => a.OfficerDecision != null
            && a.OfficerDecision.DecidedByUserId == me
            && a.OfficerDecision.DecidedAt >= since);
        var submitted = applications.Where(a => a.SubmittedByUserId == me && a.SubmittedAt >= since);
        var draftList = await drafts.OrderByDescending(d => d.UpdatedAt).Take(ListLimit).ToListAsync(cancellationToken);

        return new MyWorkResponse(
            new MyWorkCounts(
                await drafts.CountAsync(cancellationToken),
                await awaitingDecision.CountAsync(cancellationToken),
                await scoringUnavailable.CountAsync(cancellationToken),
                await awaitingSignOff.CountAsync(cancellationToken),
                await recentlyDecided.CountAsync(cancellationToken),
                await submitted.CountAsync(cancellationToken),
                await recentlyDecided.CountAsync(a => a.OfficerDecision!.Outcome == FinalDecision.Approve, cancellationToken),
                await recentlyDecided.CountAsync(a => a.OfficerDecision!.IsOverride, cancellationToken)),
            await DraftsController.Responses(db, draftList, time.GetUtcNow(), cancellationToken),
            await Oldest(awaitingDecision, cancellationToken),
            await Oldest(scoringUnavailable, cancellationToken),
            await Oldest(awaitingSignOff, cancellationToken),
            await recentlyDecided.OrderByDescending(a => a.OfficerDecision!.DecidedAt).Take(ListLimit)
                .Select(ApplicationSummary.FromApplication).ToListAsync(cancellationToken));
    }

    [HttpGet("team")]
    [Authorize(Policy = Permissions.ApplicationsAssign)]
    [ProducesResponseType<TeamWorkResponse>(StatusCodes.Status200OK)]
    public async Task<TeamWorkResponse> Team(CancellationToken cancellationToken)
    {
        var branchId = Credentials.BranchScopeOf(User)
            ?? throw new InvalidOperationException("The team queue belongs to a branch; the signed-in user has none.");
        var applications = db.Applications.AsNoTracking();
        var unassigned = applications.Where(a => a.Stage == ApplicationStage.Submitted && a.AssignedToUserId == null);
        var awaitingSignOff = applications.Where(a => a.Stage == ApplicationStage.AwaitingSignOff);
        var scoringUnavailable = applications.Where(a => a.Stage == ApplicationStage.Submitted
            && (a.Decision == null || a.Decision.Status == DecisionStatus.ScoringUnavailable));

        var open = await applications
            .Where(a => a.Stage == ApplicationStage.Submitted && a.AssignedToUserId != null)
            .GroupBy(a => a.AssignedToUserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.UserId, g => g.Count, cancellationToken);
        var waiting = await db.SignOffRequests
            .Where(s => s.Status == SignOffStatus.Pending && s.RequestedByUserId != null)
            .Join(applications, s => s.CreditApplicationId, a => a.Id, (s, a) => s.RequestedByUserId!.Value)
            .GroupBy(id => id)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.UserId, g => g.Count, cancellationToken);

        var roles = Permissions.RolesWith(Permissions.ApplicationsDecide);
        var members = await db.Users.AsNoTracking()
            .Where(u => u.BranchId == branchId && u.IsActive && roles.Contains(u.Role))
            .ToListAsync(cancellationToken);

        return new TeamWorkResponse(
            new TeamWorkCounts(
                await unassigned.CountAsync(cancellationToken),
                await awaitingSignOff.CountAsync(cancellationToken),
                await scoringUnavailable.CountAsync(cancellationToken)),
            await Oldest(unassigned, cancellationToken),
            await Oldest(awaitingSignOff, cancellationToken),
            members
                .OrderBy(u => u.DisplayName, StringComparer.Ordinal)
                .Select(u => new TeamMember(u.Id, u.DisplayName, u.Role, open.GetValueOrDefault(u.Id), waiting.GetValueOrDefault(u.Id)))
                .ToList());
    }

    private static Task<List<ApplicationSummary>> Oldest(IQueryable<CreditApplication> applications, CancellationToken cancellationToken) =>
        applications.OrderBy(a => a.SubmittedAt).ThenBy(a => a.Id).Take(ListLimit)
            .Select(ApplicationSummary.FromApplication).ToListAsync(cancellationToken);
}
