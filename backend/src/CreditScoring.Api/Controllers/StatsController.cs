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
        var applications = db.Applications.AsNoTracking();
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

        var rows = await applications
            .Select(a => new StatsRow(
                a.Decision!.Status,
                a.Decision.PBad,
                a.Decision.Recommendation,
                a.OfficerDecision == null ? null : (FinalDecision?)a.OfficerDecision.Outcome,
                a.OfficerDecision == null ? null : a.OfficerDecision.DecidedBy))
            .ToListAsync(cancellationToken);

        var shadows = await db.ShadowScores
            .AsNoTracking()
            .Join(applications, s => s.CreditApplicationId, a => a.Id,
                (s, a) => new ShadowRow(s.ModelId, s.Recommendation, a.Decision!.Recommendation))
            .ToListAsync(cancellationToken);

        return DashboardCalculator.Compute(rows, shadows);
    }
}
