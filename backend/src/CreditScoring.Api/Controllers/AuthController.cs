using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(SignInService signIn, CreditScoringDbContext db) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        switch (await signIn.SignInAsync(request.UserName!, request.Password!, cancellationToken))
        {
            case SignedIn signedIn:
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Credentials.PrincipalFor(signedIn.User));
                return MeResponse.From(signedIn.User);

            case LockedOut lockedOut:
                return ApiProblems.Create(this, StatusCodes.Status401Unauthorized, ApiProblems.LockedOut,
                    "This account is locked after too many wrong passwords.",
                    new Dictionary<string, object?> { ["lockedUntil"] = lockedOut.Until });

            case InvalidCredentials:
                return ApiProblems.Create(this, StatusCodes.Status401Unauthorized, ApiProblems.InvalidCredentials,
                    "The user name or password is not correct.");

            case var other:
                throw new InvalidOperationException($"unhandled sign-in result {other.GetType().Name}");
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var actor = Credentials.ActorOf(User);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actor.UserId, cancellationToken);
        if (user is not null)
            return MeResponse.From(user);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return ApiProblems.Create(this, StatusCodes.Status401Unauthorized, ApiProblems.Unauthenticated,
            "The signed-in user no longer exists.");
    }
}
