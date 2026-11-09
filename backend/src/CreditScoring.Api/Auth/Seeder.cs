using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Auth;

public sealed class SeedBranch
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string? City { get; init; }
}

public sealed class SeedUser
{
    public string UserName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public UserRole Role { get; init; }
    public string Password { get; init; } = "";
    public string? Branch { get; init; }
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public List<SeedBranch> Branches { get; init; } = [];
    public List<SeedUser> Users { get; init; } = [];
}

public static class Seeder
{
    public static async Task SeedAsync(CreditScoringDbContext db, SeedOptions seed, DateTimeOffset now)
    {
        var branches = await db.Branches.ToDictionaryAsync(b => b.Code);
        foreach (var wanted in seed.Branches)
        {
            var branch = Branch.Create(wanted.Code, wanted.Name, wanted.City, now);
            if (branches.TryAdd(branch.Code, branch))
                db.Branches.Add(branch);
        }

        var existing = await db.Users.Select(u => u.UserName).ToListAsync();
        foreach (var wanted in seed.Users)
        {
            Guid? branchId = wanted.Branch is null
                ? null
                : branches.GetValueOrDefault(Branch.NormalizeCode(wanted.Branch))?.Id
                    ?? throw new InvalidOperationException($"Seed user {wanted.UserName} names unknown branch {wanted.Branch}.");
            var user = User.Create(wanted.UserName, wanted.DisplayName, wanted.Role, now, branchId);
            if (existing.Contains(user.UserName))
                continue;
            user.SetPasswordHash(Credentials.Hash(user, wanted.Password));
            db.Users.Add(user);
            existing.Add(user.UserName);
        }
        await db.SaveChangesAsync();
    }
}
