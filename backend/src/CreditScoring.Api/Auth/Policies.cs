using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authorization;

namespace CreditScoring.Api.Auth;

public static class Policies
{
    public const string CanDecide = nameof(CanDecide);
    public const string CanSeeStats = nameof(CanSeeStats);
    public const string CanManageModels = nameof(CanManageModels);

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(CanDecide, p => p.RequireRole(nameof(UserRole.Officer), nameof(UserRole.Manager)));
        options.AddPolicy(CanSeeStats, p => p.RequireRole(nameof(UserRole.Manager), nameof(UserRole.Risk)));
        options.AddPolicy(CanManageModels, p => p.RequireRole(nameof(UserRole.Risk)));
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }
}
