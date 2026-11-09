using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/stats")]
[Authorize(Policy = Permissions.StatsRead)]
public class StatsController(CreditScoringDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardStats>(StatusCodes.Status200OK)]
    public async Task<DashboardStats> Get([FromQuery] StatsQuery query, CancellationToken cancellationToken)
    {
        var range = query is { From: { } from, To: { } to } ? new StatsRange(from.ToUniversalTime(), to.ToUniversalTime()) : null;
        var applications = Within(query.BranchId, query.From?.ToUniversalTime(), query.To?.ToUniversalTime());

        var rows = await Rows(applications, cancellationToken);
        var previous = range is null
            ? null
            : await Rows(Within(query.BranchId, range.Previous.From, range.Previous.To), cancellationToken);

        var shadows = await db.ShadowScores
            .AsNoTracking()
            .Join(applications, s => s.CreditApplicationId, a => a.Id,
                (s, a) => new ShadowRow(s.ModelId, s.Recommendation, a.Decision!.Recommendation))
            .ToListAsync(cancellationToken);
        var branches = await db.Branches.AsNoTracking()
            .Select(b => new BranchRef(b.Id, b.Code, b.Name))
            .ToListAsync(cancellationToken);

        return DashboardCalculator.Compute(rows, shadows, branches, range, previous);
    }

    private IQueryable<CreditApplication> Within(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to)
    {
        var applications = db.Applications.AsNoTracking();
        if (branchId is { } branch)
            applications = applications.Where(a => a.BranchId == branch);
        if (from is { } start)
            applications = applications.Where(a => a.SubmittedAt >= start);
        if (to is { } end)
            applications = applications.Where(a => a.SubmittedAt < end);
        return applications;
    }

    private static Task<List<StatsRow>> Rows(IQueryable<CreditApplication> applications, CancellationToken cancellationToken) =>
        applications
            .Select(a => new StatsRow(
                a.Decision!.Status,
                a.Decision.PBad,
                a.Decision.Recommendation,
                a.OfficerDecision == null ? null : (FinalDecision?)a.OfficerDecision.Outcome,
                a.OfficerDecision == null ? null : a.OfficerDecision.DecidedBy,
                a.SubmittedAt,
                a.BranchId,
                a.LoanOutcome == null ? null : (LoanOutcomeKind?)a.LoanOutcome.Outcome))
            .ToListAsync(cancellationToken);
}
