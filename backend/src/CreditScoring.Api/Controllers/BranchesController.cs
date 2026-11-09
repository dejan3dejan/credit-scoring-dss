using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/branches")]
public class BranchesController(CreditScoringDbContext db, TimeProvider time) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BranchResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<BranchResponse>> List(CancellationToken cancellationToken)
    {
        var branches = await db.Branches.AsNoTracking().ToListAsync(cancellationToken);
        return branches.OrderBy(b => b.Code, StringComparer.Ordinal).Select(BranchResponse.From).ToList();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BranchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var branch = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        return branch is null ? NoSuchBranch() : BranchResponse.From(branch);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.BranchesManage)]
    [ProducesResponseType<BranchResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BranchResponse>> Create(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var code = Branch.NormalizeCode(request.Code!);
        if (await db.Branches.AnyAsync(b => b.Code == code, cancellationToken))
            return CodeTaken();

        var branch = Branch.Create(code, request.Name!, request.City, time.GetUtcNow());
        db.Branches.Add(branch);
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateException ex) when (Pg.IsUniqueViolation(ex))
        {
            return CodeTaken();
        }

        return CreatedAtAction(nameof(Get), new { id = branch.Id }, BranchResponse.From(branch));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.BranchesManage)]
    [ProducesResponseType<BranchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<BranchResponse>> Update(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, branch =>
        {
            branch.Edit(request.Name!, request.City);
            return Task.FromResult<ChangeResult>(new Changed());
        }, cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.BranchesManage)]
    [ProducesResponseType<BranchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<BranchResponse>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, async branch =>
            branch.Deactivate(await db.Users.AnyAsync(u => u.BranchId == branch.Id && u.IsActive, cancellationToken)),
            cancellationToken);

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = Permissions.BranchesManage)]
    [ProducesResponseType<BranchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<BranchResponse>> Reactivate(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, branch =>
        {
            branch.Reactivate();
            return Task.FromResult<ChangeResult>(new Changed());
        }, cancellationToken);

    private async Task<ActionResult<BranchResponse>> ChangeAsync(
        Guid id,
        Func<Branch, Task<ChangeResult>> change,
        CancellationToken cancellationToken)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch is null)
            return NoSuchBranch();

        switch (await change(branch))
        {
            case Changed:
                await db.SaveChangesAsync(CancellationToken.None);
                return BranchResponse.From(branch);

            case Refused refused:
                return ApiProblems.Create(this, StatusCodes.Status409Conflict, refused.Code, refused.Message);

            case var other:
                throw new InvalidOperationException($"unhandled branch change {other.GetType().Name}");
        }
    }

    private ObjectResult NoSuchBranch() =>
        ApiProblems.Create(this, StatusCodes.Status404NotFound, ApiProblems.NotFound, "No branch with this id exists.");

    private BadRequestObjectResult CodeTaken() =>
        FieldErrors.Reject(this, "code", new FieldError("branch_code_taken"), "This branch code is already taken.");
}
