using System.Text.Json;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace CreditScoring.Tests.Contracts;

public class ApiProblemsTests
{
    private static IServiceProvider Services() => TestServices.Api().BuildServiceProvider();

    private static DefaultHttpContext Context(string path)
    {
        var context = new DefaultHttpContext { RequestServices = Services() };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;
    }

    private static Task Raise(HttpContext context, Func<CookieAuthenticationEvents, Func<RedirectContext<CookieAuthenticationOptions>, Task>> pick)
    {
        var options = new CookieAuthenticationOptions();
        CookieSetup.Configure(options);
        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var redirect = new RedirectContext<CookieAuthenticationOptions>(context, scheme, options, new AuthenticationProperties(), "/account/login?ReturnUrl=x");
        return pick(options.Events)(redirect);
    }

    [Theory]
    [InlineData(400, "validation_failed")]
    [InlineData(401, "unauthenticated")]
    [InlineData(403, "forbidden")]
    [InlineData(404, "not_found")]
    [InlineData(409, "conflict")]
    public void Problems_get_a_default_code_from_their_status(int status, string code)
    {
        var problem = Services().GetRequiredService<ProblemDetailsFactory>().CreateProblemDetails(new DefaultHttpContext(), status);

        Assert.Equal(code, problem.Extensions["code"]);
    }

    [Fact]
    public void Validation_problems_get_the_validation_code()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("userName", "The User name field is required.");

        var problem = Services().GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(new DefaultHttpContext(), state);

        Assert.Equal("validation_failed", problem.Extensions["code"]);
    }

    [Fact]
    public void A_specific_code_replaces_the_default()
    {
        var problem = new ProblemDetails { Status = 401 };
        problem.Extensions["code"] = "locked_out";

        ApiProblems.AddDefaultCode(new ProblemDetailsContext { HttpContext = new DefaultHttpContext(), ProblemDetails = problem });

        Assert.Equal("locked_out", problem.Extensions["code"]);
    }

    [Fact]
    public async Task Missing_sign_in_under_api_is_a_401_problem_not_a_redirect()
    {
        var context = Context("/api/applications");

        await Raise(context, e => e.OnRedirectToLogin);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.Equal("unauthenticated", (await Body(context)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Missing_permission_under_api_is_a_403_problem()
    {
        var context = Context("/api/models");

        await Raise(context, e => e.OnRedirectToAccessDenied);

        Assert.Equal(403, context.Response.StatusCode);
        Assert.Equal("forbidden", (await Body(context)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Pages_outside_api_still_redirect_to_the_login_page()
    {
        var context = Context("/applications");

        await Raise(context, e => e.OnRedirectToLogin);

        Assert.Equal(302, context.Response.StatusCode);
        Assert.Equal("/account/login?ReturnUrl=x", context.Response.Headers.Location.ToString());
    }
}
