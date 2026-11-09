using System.Security.Claims;
using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Auth;

public static class CookieSetup
{
    public const string CookieName = "credit_scoring";

    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context => RedirectOrProblem(context, StatusCodes.Status401Unauthorized);
        options.Events.OnRedirectToAccessDenied = context => RedirectOrProblem(context, StatusCodes.Status403Forbidden);
        options.Events.OnValidatePrincipal = ValidateAsync;
    }

    private static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        if (context.Principal is not { } principal)
            return;

        User? user = null;
        if (Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<CreditScoringDbContext>();
            user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, context.HttpContext.RequestAborted);
        }

        switch (Credentials.Check(principal, user))
        {
            case SessionCheck.Reject:
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                break;
            case SessionCheck.Refresh:
                context.ReplacePrincipal(Credentials.PrincipalFor(user!));
                context.ShouldRenew = true;
                break;
        }
    }

    private static Task RedirectOrProblem(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            return Results.Problem(statusCode: status).ExecuteAsync(context.HttpContext);

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
