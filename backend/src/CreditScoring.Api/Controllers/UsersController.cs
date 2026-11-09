using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = Permissions.UsersManage)]
public class UsersController(CreditScoringDbContext db, TimeProvider time) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UserResponse>> List(CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().ToListAsync(cancellationToken);
        var now = time.GetUtcNow();
        return users
            .OrderBy(u => u.DisplayName, StringComparer.Ordinal)
            .ThenBy(u => u.UserName, StringComparer.Ordinal)
            .Select(u => UserResponse.From(u, now))
            .ToList();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null ? NoSuchUser() : UserResponse.From(user, time.GetUtcNow());
    }

    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var userName = Domain.User.NormalizeUserName(request.UserName!);
        if (await db.Users.AnyAsync(u => u.UserName == userName, cancellationToken))
            return UserNameTaken();

        var now = time.GetUtcNow();
        var user = Domain.User.Create(userName, request.DisplayName!, request.Role!.Value, now);
        user.Edit(request.DisplayName!, request.Email, request.Role.Value, request.PreferredLanguage ?? Languages.English);
        user.ResetPassword(Credentials.Hash(user, request.TemporaryPassword!));
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateException ex) when (Pg.IsUniqueViolation(ex))
        {
            return UserNameTaken();
        }

        return CreatedAtAction(nameof(Get), new { id = user.Id }, UserResponse.From(user, now));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<UserResponse>> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, user => UserAdministration.Update(
            user, Credentials.ActorOf(User).UserId, request.DisplayName!, request.Email, request.Role!.Value, request.PreferredLanguage!),
            cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<UserResponse>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, user => UserAdministration.Deactivate(user, Credentials.ActorOf(User).UserId), cancellationToken);

    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<UserResponse>> Reactivate(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, user =>
        {
            user.Reactivate();
            return new UserChanged();
        }, cancellationToken);

    [HttpPost("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
            return NoSuchUser();

        user.ResetPassword(Credentials.Hash(user, request.TemporaryPassword!));
        await db.SaveChangesAsync(CancellationToken.None);
        return NoContent();
    }

    private async Task<ActionResult<UserResponse>> ChangeAsync(Guid id, Func<Domain.User, UserChange> change, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
            return NoSuchUser();

        switch (change(user))
        {
            case UserChanged:
                await db.SaveChangesAsync(CancellationToken.None);
                return UserResponse.From(user, time.GetUtcNow());

            case UserRefused { Field: { } field } refused:
                return FieldErrors.Reject(this, field, new FieldError(refused.Code), refused.Message);

            case UserRefused refused:
                return ApiProblems.Create(this, StatusCodes.Status409Conflict, refused.Code, refused.Message);

            case var other:
                throw new InvalidOperationException($"unhandled user change {other.GetType().Name}");
        }
    }

    private ObjectResult NoSuchUser() =>
        ApiProblems.Create(this, StatusCodes.Status404NotFound, ApiProblems.NotFound, "No user with this id exists.");

    private BadRequestObjectResult UserNameTaken() =>
        FieldErrors.Reject(this, "userName", new FieldError("user_name_taken"), "This user name is already taken.");
}
