using CreditScoring.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Data;

public class CreditScoringDbContextTests
{
    private static CreditScoringDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CreditScoringDbContext>().UseNpgsql("Host=unused").Options);

    [Fact]
    public void Migrations_match_the_model()
    {
        using var context = CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Enums_are_stored_as_strings()
    {
        using var context = CreateContext();

        var enumProperties = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType).IsEnum)
            .ToList();

        Assert.Equal(14, enumProperties.Count);
        Assert.All(enumProperties, p => Assert.Equal(typeof(string), p.GetProviderClrType()));
    }
}
