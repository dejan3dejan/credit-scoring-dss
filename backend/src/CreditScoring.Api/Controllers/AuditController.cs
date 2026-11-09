using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = Permissions.AuditRead)]
public class AuditController(CreditScoringDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<AuditEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<PagedResult<AuditEntryResponse>> List([FromQuery] AuditQuery query, CancellationToken cancellationToken)
    {
        var entries = db.AuditEntries.AsNoTracking();

        if (query.From is { } from)
        {
            var fromUtc = from.ToUniversalTime();
            entries = entries.Where(e => e.At >= fromUtc);
        }
        if (query.To is { } to)
        {
            var toUtc = to.ToUniversalTime();
            entries = entries.Where(e => e.At < toUtc);
        }
        if (query.UserId is { } userId)
            entries = entries.Where(e => e.UserId == userId);
        if (!string.IsNullOrWhiteSpace(query.Entity))
            entries = entries.Where(e => e.Entity == query.Entity);
        if (!string.IsNullOrWhiteSpace(query.EntityId))
            entries = entries.Where(e => e.EntityId == query.EntityId);
        if (query.Action is { } action)
            entries = entries.Where(e => e.Action == action);

        var total = await entries.CountAsync(cancellationToken);
        var page = await entries
            .OrderByDescending(e => e.At)
            .ThenByDescending(e => e.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditEntryResponse>(page.Select(AuditEntryResponse.From).ToList(), query.Page, query.PageSize, total);
    }
}
