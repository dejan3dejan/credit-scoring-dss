using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Auth;

public sealed class HttpAuditContext(IHttpContextAccessor accessor) : IAuditContext
{
    public Actor? Actor =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? Credentials.ActorOf(user) : null;

    public string? TraceId => accessor.HttpContext?.TraceIdentifier;
}
