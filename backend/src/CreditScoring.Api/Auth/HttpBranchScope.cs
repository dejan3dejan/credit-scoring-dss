using CreditScoring.Api.Data;

namespace CreditScoring.Api.Auth;

public sealed class HttpBranchScope(IHttpContextAccessor accessor) : IBranchScope
{
    public Guid? BranchId =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? Credentials.BranchScopeOf(user) : null;
}
