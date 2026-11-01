using System.Net;
using CreditScoring.Api.Auth;
using CreditScoring.Tests.Ml;
using Microsoft.AspNetCore.Http;

namespace CreditScoring.Tests.Auth;

public class CookieForwardingHandlerTests
{
    private static async Task<HttpRequestMessage> Send(HttpContext? context)
    {
        var stub = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var handler = new CookieForwardingHandler(new HttpContextAccessor { HttpContext = context }) { InnerHandler = stub };
        using var client = new HttpClient(handler);
        await client.GetAsync("http://app.test/api/applications");
        return stub.LastRequest!;
    }

    [Fact]
    public async Task Cookie_of_the_current_request_is_forwarded()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "credit_scoring=abc; other=1";

        var request = await Send(context);

        Assert.Equal(["credit_scoring=abc; other=1"], request.Headers.GetValues("Cookie"));
    }

    [Fact]
    public async Task Nothing_is_added_outside_a_request()
    {
        var request = await Send(null);

        Assert.False(request.Headers.Contains("Cookie"));
    }
}
