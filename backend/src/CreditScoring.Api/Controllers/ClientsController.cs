using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController(CreditScoringDbContext db, TimeProvider time) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.CanDecide)]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientResponse>> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (request.DateOfBirth > DateOnly.FromDateTime(now.UtcDateTime))
            return FieldErrors.Reject(this, "dateOfBirth", new FieldError("in_the_future"), "Date of birth cannot be in the future.");

        var client = Client.Create(request.FullName!, request.DateOfBirth, Credentials.ActorOf(User), now);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);

        return CreatedAtAction(nameof(Get), new { id = client.Id }, ClientResponse.From(client));
    }

    [HttpGet]
    [ProducesResponseType<PagedResult<ClientSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ClientSummary>>> List([FromQuery] ClientQuery query, CancellationToken cancellationToken)
    {
        var clients = db.Clients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = Like.Contains(query.Search);
            clients = clients.Where(c =>
                EF.Functions.ILike(c.FullName, pattern, Like.Escape) || EF.Functions.ILike(c.ClientNumber, pattern, Like.Escape));
        }

        var total = await clients.CountAsync(cancellationToken);
        var items = await clients
            .OrderBy(c => c.FullName)
            .ThenBy(c => c.ClientNumber)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new ClientSummary(
                c.Id,
                c.ClientNumber,
                c.FullName,
                c.DateOfBirth,
                db.Applications.Count(a => a.ClientId == c.Id),
                db.Applications.Where(a => a.ClientId == c.Id).Max(a => (DateTimeOffset?)a.SubmittedAt)))
            .ToListAsync(cancellationToken);

        return new PagedResult<ClientSummary>(items, query.Page, query.PageSize, total);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClientDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
            return NotFound();

        var applications = await db.Applications
            .AsNoTracking()
            .Where(a => a.ClientId == id)
            .OrderByDescending(a => a.SubmittedAt)
            .Select(ApplicationSummary.FromApplication)
            .ToListAsync(cancellationToken);

        return new ClientDetail(client.Id, client.ClientNumber, client.FullName, client.DateOfBirth, client.CreatedAt, applications);
    }
}
