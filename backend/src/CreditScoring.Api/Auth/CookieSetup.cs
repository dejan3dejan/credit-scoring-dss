using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

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
    }

    private static Task RedirectOrProblem(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            return Results.Problem(statusCode: status).ExecuteAsync(context.HttpContext);

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
