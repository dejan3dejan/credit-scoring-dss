using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Auth;

public sealed class SeedUser
{
    public string UserName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public UserRole Role { get; init; }
    public string Password { get; init; } = "";
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public List<SeedUser> Users { get; init; } = [];
}

public static class UserSeeder
{
    public static async Task SeedAsync(CreditScoringDbContext db, IEnumerable<SeedUser> users, DateTimeOffset now)
    {
        var existing = await db.Users.Select(u => u.UserName).ToListAsync();
        foreach (var seed in users)
        {
            var user = User.Create(seed.UserName, seed.DisplayName, seed.Role, now);
            if (existing.Contains(user.UserName))
                continue;
            user.SetPasswordHash(Credentials.Hash(user, seed.Password));
            db.Users.Add(user);
            existing.Add(user.UserName);
        }
        await db.SaveChangesAsync();
    }
}
