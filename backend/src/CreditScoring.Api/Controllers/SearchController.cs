using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/search")]
[Authorize(Policy = Permissions.ClientsRead)]
public class SearchController(CreditScoringDbContext db) : ControllerBase
{
    private const int Limit = 10;

    [HttpGet]
    [ProducesResponseType<SearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<SearchResponse> Get([FromQuery] SearchQuery query, CancellationToken cancellationToken)
    {
        var text = query.Q!.Trim();
        var exact = text.ToUpperInvariant();
        var pattern = Like.Contains(text);

        var clients = db.Clients.AsNoTracking()
            .Where(c => EF.Functions.ILike(c.FullName, pattern, Like.Escape) || EF.Functions.ILike(c.ClientNumber, pattern, Like.Escape))
            .OrderBy(c => c.ClientNumber == exact ? 0 : 1)
            .ThenBy(c => c.FullName)
            .ThenBy(c => c.ClientNumber)
            .Take(Limit);
        var applications = db.Applications.AsNoTracking()
            .Where(a => EF.Functions.ILike(a.Reference, pattern, Like.Escape) || EF.Functions.ILike(a.ApplicantName, pattern, Like.Escape))
            .OrderBy(a => a.Reference == exact ? 0 : 1)
            .ThenByDescending(a => a.SubmittedAt)
            .Take(Limit);

        return new SearchResponse(
            await ClientsController.Summaries(clients, db).ToListAsync(cancellationToken),
            await applications.Select(ApplicationSummary.FromApplication).ToListAsync(cancellationToken));
    }
}
