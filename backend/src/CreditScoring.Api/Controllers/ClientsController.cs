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
    [Authorize(Policy = Permissions.ClientsWrite)]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientResponse>> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (FutureBirthDate(request, now) is { } future)
            return future;

        var branchId = Credentials.BranchScopeOf(User)
            ?? throw new InvalidOperationException("Only users who belong to a branch can create clients.");
        var client = Client.Create(request.FullName!, request.DateOfBirth, branchId, Credentials.ActorOf(User), now, request.Contact());
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);

        return CreatedAtAction(nameof(Get), new { id = client.Id }, ClientResponse.From(client));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ClientsRead)]
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
        if (query.Active is { } active)
            clients = clients.Where(c => c.IsActive == active);

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
                db.Applications.Where(a => a.ClientId == c.Id).Max(a => (DateTimeOffset?)a.SubmittedAt),
                c.BranchId,
                c.IsActive,
                c.AnonymisedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ClientSummary>(items, query.Page, query.PageSize, total);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.ClientsRead)]
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

        return ClientDetail.From(client, applications);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.ClientsWrite)]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientResponse>> Update(Guid id, UpdateClientRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (FutureBirthDate(request, now) is { } future)
            return future;

        return await ChangeAsync(id, client => client.Edit(request.FullName!, request.DateOfBirth, request.Contact(), now), cancellationToken);
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.ClientsWrite)]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ClientResponse>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, client => client.Deactivate(), cancellationToken);

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = Permissions.ClientsWrite)]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ClientResponse>> Reactivate(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, client => client.Reactivate(), cancellationToken);

    [HttpPost("{id:guid}/anonymise")]
    [Authorize(Policy = Permissions.ClientsAnonymise)]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientResponse>> Anonymise(Guid id, CancellationToken cancellationToken)
    {
        var applications = await db.Applications.Where(a => a.ClientId == id).ToListAsync(cancellationToken);
        return await ChangeAsync(id, client => ClientAnonymisation.Apply(client, applications, time.GetUtcNow()), cancellationToken);
    }

    private async Task<ActionResult<ClientResponse>> ChangeAsync(Guid id, Func<Client, ChangeResult> change, CancellationToken cancellationToken)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
            return ApiProblems.Create(this, StatusCodes.Status404NotFound, ApiProblems.NotFound, "No client with this id exists.");

        switch (change(client))
        {
            case Changed:
                await db.SaveChangesAsync(CancellationToken.None);
                return ClientResponse.From(client);

            case Refused refused:
                return ApiProblems.Create(this, StatusCodes.Status409Conflict, refused.Code, refused.Message);

            case var other:
                throw new InvalidOperationException($"unhandled client change {other.GetType().Name}");
        }
    }

    private BadRequestObjectResult? FutureBirthDate(ClientDetailsRequest request, DateTimeOffset now) =>
        request.DateOfBirth > DateOnly.FromDateTime(now.UtcDateTime)
            ? FieldErrors.Reject(this, "dateOfBirth", new FieldError("in_the_future"), "Date of birth cannot be in the future.")
            : null;
}
