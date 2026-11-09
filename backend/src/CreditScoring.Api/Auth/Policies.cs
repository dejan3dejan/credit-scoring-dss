using Microsoft.AspNetCore.Authorization;

namespace CreditScoring.Api.Auth;

public static class Policies
{
    public static void Configure(AuthorizationOptions options)
    {
        foreach (var permission in Permissions.All)
            options.AddPolicy(permission, p => p.RequireRole(Permissions.RolesWith(permission).Select(r => r.ToString())));
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }
}
