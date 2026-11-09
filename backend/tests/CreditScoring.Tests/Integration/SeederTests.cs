using CreditScoring.Api.Auth;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class SeederTests(DatabaseFixture database)
{
    private static SeedUser Seed(string userName, string? branch = "HQ", UserRole role = UserRole.Manager) =>
        new() { UserName = userName, DisplayName = "Seeded " + userName, Role = role, Password = "pw-" + userName, Branch = branch };

    private static string NewName(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task Missing_users_are_added_once_with_hashed_passwords_and_their_branch()
    {
        var name = NewName("seed-");

        await using (var db = database.CreateContext())
        {
            await Seeder.SeedAsync(db, new SeedOptions { Users = [Seed(name)] }, TestData.Now);
            await Seeder.SeedAsync(db, new SeedOptions { Users = [Seed(name)] }, TestData.Now);
        }

        await using var check = database.CreateContext();
        var user = await check.Users.SingleAsync(u => u.UserName == name);
        Assert.Equal((UserRole.Manager, database.HeadOfficeId), (user.Role, user.BranchId));
        Assert.True(Credentials.Verify(user, "pw-" + name));
    }

    [Fact]
    public async Task Missing_branches_are_added_once_before_the_users_that_name_them()
    {
        var code = NewName("S").ToUpperInvariant();
        var user = NewName("seed-");
        var seed = new SeedOptions
        {
            Branches = [new SeedBranch { Code = code, Name = "Seeded branch", City = "Niš" }],
            Users = [Seed(user, code, UserRole.Officer)],
        };

        await using (var db = database.CreateContext())
        {
            await Seeder.SeedAsync(db, seed, TestData.Now);
            await Seeder.SeedAsync(db, seed, TestData.Now);
        }

        await using var check = database.CreateContext();
        var branch = await check.Branches.SingleAsync(b => b.Code == code);
        Assert.Equal(("Seeded branch", "Niš"), (branch.Name, branch.City));
        Assert.Equal(branch.Id, (await check.Users.SingleAsync(u => u.UserName == user)).BranchId);
    }

    [Fact]
    public async Task A_user_naming_an_unknown_branch_stops_the_seed()
    {
        await using var db = database.CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seeder.SeedAsync(db, new SeedOptions { Users = [Seed(NewName("seed-"), "NOWHERE")] }, TestData.Now));
    }
}
