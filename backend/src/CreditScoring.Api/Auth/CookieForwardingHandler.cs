namespace CreditScoring.Api.Auth;

public class CookieForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cookie = accessor.HttpContext?.Request.Headers.Cookie.ToString();
        if (!string.IsNullOrEmpty(cookie))
            request.Headers.Add("Cookie", cookie);
        return base.SendAsync(request, cancellationToken);
    }
}
