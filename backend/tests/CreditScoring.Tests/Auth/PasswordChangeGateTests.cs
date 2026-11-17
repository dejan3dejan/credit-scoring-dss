using System.Text.Json;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CreditScoring.Tests.Auth;

public class PasswordChangeGateTests
{
    private static DefaultHttpContext Request(string path, bool? mustChange, bool allowed = false)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider(),
        };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (mustChange is { } flag)
            PasswordChangeGate.Record(context, flag);
        var metadata = allowed ? new EndpointMetadataCollection(new AllowedBeforePasswordChangeAttribute()) : EndpointMetadataCollection.Empty;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));
        return context;
    }

    private static async Task<bool> Passes(HttpContext context)
    {
        var reached = false;
        await PasswordChangeGate.InvokeAsync(context, _ =>
        {
            reached = true;
            return Task.CompletedTask;
        });
        return reached;
    }

    [Fact]
    public async Task A_user_who_must_change_the_password_is_stopped_at_the_api()
    {
        var context = Request("/api/applications", mustChange: true);

        Assert.False(await Passes(context));

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var problem = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(PasswordChangeGate.Code, problem.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("/api/auth/password", true, true)]
    [InlineData("/api/applications", false, false)]
    [InlineData("/api/applications", null, false)]
    [InlineData("/app/change-password", true, false)]
    public async Task Allowed_endpoints_other_users_and_the_app_shell_pass(string path, bool? mustChange, bool allowed)
    {
        Assert.True(await Passes(Request(path, mustChange, allowed)));
    }

    [Fact]
    public void Every_auth_endpoint_is_allowed_before_the_change()
    {
        Assert.True(Attribute.IsDefined(typeof(AuthController), typeof(AllowedBeforePasswordChangeAttribute)));
    }
}
