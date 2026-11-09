using System.Text.Json;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/drafts")]
[Authorize(Policy = Permissions.ApplicationsWrite)]
public class DraftsController(CreditScoringDbContext db, TimeProvider time) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DraftResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<DraftResponse>> List(CancellationToken cancellationToken)
    {
        var me = Credentials.ActorOf(User).UserId;
        var drafts = await db.Drafts.AsNoTracking()
            .Include(d => d.Client)
            .Where(d => d.CreatedByUserId == me && d.Status == DraftStatus.Open)
            .OrderByDescending(d => d.UpdatedAt)
            .ThenByDescending(d => d.Id)
            .ToListAsync(cancellationToken);
        return drafts.Select(DraftResponse.From).ToList();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DraftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DraftResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var draft = await Own(db.Drafts.AsNoTracking(), id, cancellationToken);
        return draft is null ? NotFound() : DraftResponse.From(draft);
    }

    [HttpPost]
    [ProducesResponseType<DraftResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DraftResponse>> Create([FromBody] JsonElement answers, CancellationToken cancellationToken)
    {
        var (form, client, error) = await ReadAsync(answers, cancellationToken);
        if (error is not null)
            return error;

        var draft = ApplicationDraft.Start(client!, Serialize(form!), Credentials.ActorOf(User), time.GetUtcNow());
        db.Drafts.Add(draft);
        await db.SaveChangesAsync(CancellationToken.None);
        return CreatedAtAction(nameof(Get), new { id = draft.Id }, DraftResponse.From(draft));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<DraftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DraftResponse>> Update(Guid id, [FromBody] JsonElement answers, CancellationToken cancellationToken)
    {
        var draft = await Own(db.Drafts, id, cancellationToken);
        if (draft is null)
            return NotFound();
        var (form, client, error) = await ReadAsync(answers, cancellationToken);
        if (error is not null)
            return error;

        return await SaveAsync(draft, draft.Replace(client!, Serialize(form!), time.GetUtcNow()));
    }

    [HttpPost("{id:guid}/discard")]
    [ProducesResponseType<DraftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DraftResponse>> Discard(Guid id, CancellationToken cancellationToken)
    {
        var draft = await Own(db.Drafts, id, cancellationToken);
        return draft is null ? NotFound() : await SaveAsync(draft, draft.Discard(time.GetUtcNow()));
    }

    private Task<ApplicationDraft?> Own(IQueryable<ApplicationDraft> drafts, Guid id, CancellationToken cancellationToken)
    {
        var me = Credentials.ActorOf(User).UserId;
        return drafts.Include(d => d.Client).FirstOrDefaultAsync(d => d.Id == id && d.CreatedByUserId == me, cancellationToken);
    }

    private async Task<ActionResult<DraftResponse>> SaveAsync(ApplicationDraft draft, ChangeResult result)
    {
        switch (result)
        {
            case Changed:
                await db.SaveChangesAsync(CancellationToken.None);
                return DraftResponse.From(draft);
            case Refused refused:
                return ApiProblems.Create(this, StatusCodes.Status409Conflict, refused.Code, refused.Message);
            default:
                throw new InvalidOperationException($"unhandled draft change {result.GetType().Name}");
        }
    }

    private async Task<(CreateApplicationRequest? Form, Client? Client, ActionResult? Error)> ReadAsync(
        JsonElement answers,
        CancellationToken cancellationToken)
    {
        if (answers.ValueKind != JsonValueKind.Object)
            return (null, null, Invalid("$"));

        CreateApplicationRequest form;
        try
        {
            form = answers.Deserialize<CreateApplicationRequest>(ApiJson.Options)
                ?? throw new JsonException("a draft is an object", "$", null, null);
        }
        catch (JsonException ex)
        {
            return (null, null, Invalid(ex.Path ?? "$"));
        }

        if (form.ClientId is not { } clientId)
            return (null, null, FieldErrors.Reject(this, "clientId", new FieldError(FieldError.Required), "Choose the client first."));
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client is null)
            return (null, null, FieldErrors.Reject(this, "clientId", new FieldError("client_not_found"), "No client with this id exists."));
        if (!client.IsActive)
            return (null, null, FieldErrors.Reject(this, "clientId", new FieldError("client_inactive"), "This client is inactive."));

        return (form with { DraftId = null }, client, null);
    }

    private BadRequestObjectResult Invalid(string path) =>
        FieldErrors.Reject(this, path, new FieldError(FieldError.InvalidValue), "This value does not fit the application form.");

    private static string Serialize(CreateApplicationRequest form) => JsonSerializer.Serialize(form, ApiJson.Options);
}
