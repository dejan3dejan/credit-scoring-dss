using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class BranchScopeTests(DatabaseFixture database) : IAsyncLifetime
{
    private readonly FakeMlClient ml = new() { Result = new MlScore(0.1, TestData.ModelId) };
    private readonly ManualTimeProvider clock = new(TestData.Now);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> Applications<T>(User user, Func<ApplicationsController, Task<T>> action)
    {
        await using var db = database.CreateContext(user.AsActor(), Credentials.BranchScopeOf(Credentials.PrincipalFor(user)));
        return await action(new ApplicationsController(db, TestServices.DecisionService(ml, clock), clock)
        {
            ControllerContext = TestServices.SignedIn(user),
        });
    }

    private async Task<User> NewUser(UserRole role, Guid? branchId = null)
    {
        var user = User.Create($"u-{Guid.NewGuid():N}"[..20], role.ToString(), role, TestData.Now, branchId);
        await using var db = database.CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<Guid> ClientOf(User officer, string name)
    {
        await using var db = database.CreateContext();
        var client = Client.Create(name, null, officer.BranchId!.Value, officer.AsActor(), TestData.Now);
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client.Id;
    }

    private async Task<ApplicationResponse> Submit(User officer, string name)
    {
        var clientId = await ClientOf(officer, name);
        var response = await Applications(officer, c => c.Create(TestData.ValidRequest() with { ClientId = clientId }, CancellationToken.None));
        return Assert.IsType<ApplicationResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
    }

    [Fact]
    public async Task An_application_takes_its_clients_branch()
    {
        var mine = await Submit(database.Officer, "Ana Petrović");
        var theirs = await Submit(database.OtherOfficer, "Marko Ilić");

        Assert.Equal(database.HeadOfficeId, mine.BranchId);
        Assert.Equal(database.OtherBranchId, theirs.BranchId);
    }

    [Fact]
    public async Task Officers_see_and_act_only_on_their_branchs_applications()
    {
        var mine = await Submit(database.Officer, "Ana Petrović");
        var theirs = await Submit(database.OtherOfficer, "Marko Ilić");

        var listed = (await Applications(database.Officer, c => c.List(new HistoryQuery(), CancellationToken.None))).Value!.Items;
        var get = await Applications(database.Officer, c => c.Get(theirs.Id, CancellationToken.None));
        var decide = await Applications(database.Officer, c => c.Decide(theirs.Id, new RecordDecisionRequest { Outcome = FinalDecision.Approve }, CancellationToken.None));
        var rescore = await Applications(database.Officer, c => c.Rescore(theirs.Id, CancellationToken.None));

        Assert.Equal([mine.Id], listed.Select(i => i.Id));
        Assert.Equal(database.HeadOfficeId, listed.Single().BranchId);
        Assert.IsType<NotFoundResult>(get.Result);
        Assert.IsType<NotFoundResult>(decide.Result);
        Assert.IsType<NotFoundResult>(rescore.Result);
    }

    [Fact]
    public async Task A_client_of_another_branch_cannot_get_an_application_here()
    {
        var theirClient = await ClientOf(database.OtherOfficer, "Marko Ilić");

        var response = await Applications(database.Officer, c => c.Create(TestData.ValidRequest() with { ClientId = theirClient }, CancellationToken.None));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
        Assert.Equal(["client_not_found"], TestServices.FieldCodes(problem, "clientId"));
        Assert.Empty(ml.ScoredModels);
    }

    [Fact]
    public async Task Risk_sees_every_branch()
    {
        await Submit(database.Officer, "Ana Petrović");
        var theirs = await Submit(database.OtherOfficer, "Marko Ilić");
        var risk = await NewUser(UserRole.Risk);

        var listed = (await Applications(risk, c => c.List(new HistoryQuery(), CancellationToken.None))).Value!.Items;
        var get = await Applications(risk, c => c.Get(theirs.Id, CancellationToken.None));

        Assert.Equal(2, listed.Count);
        Assert.Equal(theirs.Id, get.Value!.Id);
    }

    [Fact]
    public async Task A_managers_dashboard_counts_only_their_branch()
    {
        await Submit(database.Officer, "Ana Petrović");
        await Submit(database.OtherOfficer, "Marko Ilić");
        await Submit(database.OtherOfficer, "Mira Kovač");
        var manager = await NewUser(UserRole.Manager, database.HeadOfficeId);
        var risk = await NewUser(UserRole.Risk);

        async Task<int> Count(User user)
        {
            await using var db = database.CreateContext(user.AsActor(), Credentials.BranchScopeOf(Credentials.PrincipalFor(user)));
            return (await new StatsController(db).Get(new StatsQuery(), CancellationToken.None)).Applications;
        }

        Assert.Equal(1, await Count(manager));
        Assert.Equal(3, await Count(risk));
    }

    [Fact]
    public async Task Me_names_the_users_branch()
    {
        await using var db = database.CreateContext();
        var auth = new AuthController(new SignInService(db, clock), db)
        {
            ControllerContext = TestServices.SignedIn(database.Officer),
        };

        var me = (await auth.Me(CancellationToken.None)).Value!;

        Assert.Equal((database.HeadOfficeId, "HQ", "Head office"), (me.Branch!.Id, me.Branch.Code, me.Branch.Name));
    }
}
