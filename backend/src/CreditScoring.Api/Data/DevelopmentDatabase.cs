using CreditScoring.Api.Auth;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Data;

public static class DevelopmentDatabase
{
    public static async Task PrepareAsync(CreditScoringDbContext db, SeedOptions seed, DateTimeOffset now)
    {
        await db.Database.MigrateAsync();
        await Seeder.SeedAsync(db, seed, now);
    }
}
