using CreditScoring.Api.Auth;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Data;

public static class DevelopmentDatabase
{
    public static async Task PrepareAsync(CreditScoringDbContext db, IEnumerable<SeedUser> users, DateTimeOffset now)
    {
        await db.Database.MigrateAsync();
        await UserSeeder.SeedAsync(db, users, now);
    }
}
