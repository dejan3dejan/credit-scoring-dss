using System.Security.Claims;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace CreditScoring.Api.Auth;

public static class Credentials
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static string Hash(User user, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return Hasher.HashPassword(user, password);
    }

    public static bool Verify(User user, string password) =>
        !string.IsNullOrEmpty(user.PasswordHash)
        && !string.IsNullOrEmpty(password)
        && Hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;

    public static ClaimsPrincipal PrincipalFor(User user)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
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
