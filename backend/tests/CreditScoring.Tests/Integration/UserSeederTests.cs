using CreditScoring.Api.Auth;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class UserSeederTests(DatabaseFixture database)
{
    private static SeedUser Seed(string userName) =>
        new() { UserName = userName, DisplayName = "Seeded " + userName, Role = UserRole.Manager, Password = "pw-" + userName };

    [Fact]
    public async Task Missing_users_are_added_once_with_hashed_passwords()
    {
        var name = "seed-" + Guid.NewGuid().ToString("N")[..8];

        await using (var db = database.CreateContext())
        {
            await UserSeeder.SeedAsync(db, [Seed(name)], TestData.Now);
            await UserSeeder.SeedAsync(db, [Seed(name)], TestData.Now);
        }

        await using var check = database.CreateContext();
        var user = await check.Users.SingleAsync(u => u.UserName == name);
        Assert.Equal(UserRole.Manager, user.Role);
        Assert.True(Credentials.Verify(user, "pw-" + name));
    }
}
