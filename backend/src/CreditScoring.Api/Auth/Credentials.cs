using System.Security.Claims;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace CreditScoring.Api.Auth;

public enum SessionCheck
{
    Valid = 1,
    Refresh = 2,
    Reject = 3,
}

public static class Credentials
{
    public const string StampClaim = "security_stamp";
    public const string BranchClaim = "branch_id";

    public static SessionCheck Check(ClaimsPrincipal principal, User? user)
    {
        if (user is null || !user.IsActive || principal.FindFirstValue(StampClaim) != user.SecurityStamp.ToString())
            return SessionCheck.Reject;

        var current = principal.FindFirstValue(ClaimTypes.Role) == user.Role.ToString()
            && principal.FindFirstValue(ClaimTypes.Name) == user.DisplayName
            && principal.FindFirstValue(BranchClaim) == user.BranchId?.ToString();
        return current ? SessionCheck.Valid : SessionCheck.Refresh;
    }

    private static readonly PasswordHasher<User> Hasher = new();

    public static string Hash(User user, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return Hasher.HashPassword(user, password);
    }

    private static readonly Lazy<User> Nobody = new(() =>
    {
        var user = User.Create("nobody", "Nobody", UserRole.Officer, DateTimeOffset.UnixEpoch);
        user.SetPasswordHash(Hasher.HashPassword(user, Guid.NewGuid().ToString()));
        return user;
    });

    public static bool Verify(User user, string password) =>
        !string.IsNullOrEmpty(user.PasswordHash)
        && !string.IsNullOrEmpty(password)
        && Hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;

    public static void VerifyAgainstNobody(string password) => Verify(Nobody.Value, password);

    public static ClaimsPrincipal PrincipalFor(User user)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(StampClaim, user.SecurityStamp.ToString()),
                .. user.BranchId is { } branchId ? [new Claim(BranchClaim, branchId.ToString())] : Array.Empty<Claim>(),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public static Guid? BranchScopeOf(ClaimsPrincipal principal)
    {
        var role = principal.FindFirstValue(ClaimTypes.Role);
        if (role != nameof(UserRole.Officer) && role != nameof(UserRole.Manager))
            return null;
        return Guid.TryParse(principal.FindFirstValue(BranchClaim), out var branchId)
            ? branchId
            : throw new InvalidOperationException($"A signed-in {role} has no branch; refusing to show every branch.");
    }

    public static Actor ActorOf(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = principal.FindFirstValue(ClaimTypes.Name);
        if (!Guid.TryParse(id, out var userId) || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("The request has no signed-in user; this endpoint must require authentication.");
        return new Actor(userId, name);
    }
}
