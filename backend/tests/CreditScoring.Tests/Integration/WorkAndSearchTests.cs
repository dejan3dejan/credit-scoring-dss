using System.Text.Json;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class WorkAndSearchTests(DatabaseFixture database) : IAsyncLifetime
{
    private readonly FakeMlClient ml = new();
    private readonly ManualTimeProvider clock = new(TestData.Now);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private CreditScoringDbContextScope Scope(User user) =>
        new(database.CreateContext(user.AsActor(), Credentials.BranchScopeOf(Credentials.PrincipalFor(user))), user);

    private sealed record CreditScoringDbContextScope(Api.Data.CreditScoringDbContext Db, User User) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private async Task<T> Applications<T>(User user, Func<ApplicationsController, Task<T>> action)
    {
        await using var scope = Scope(user);
        return await action(new ApplicationsController(scope.Db, TestServices.DecisionService(ml, clock), clock) { ControllerContext = TestServices.SignedIn(user) });
    }

    private async Task<Client> NewClient(string name, User officer)
    {
        await using var db = database.CreateContext();
        var client = Client.Create(name, null, officer.BranchId!.Value, officer.AsActor(), TestData.Now);
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client;
    }

    private async Task<ApplicationResponse> Submit(User officer, Client client, MlScoreResult score, int loanAmount = 12_000)
    {
        ml.Result = score;
        var response = await Applications(officer, c => c.Create(TestData.ValidRequest() with { ClientId = client.Id, LoanAmount = loanAmount }, CancellationToken.None));
        return Assert.IsType<ApplicationResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
    }

    private Task<ActionResult<ApplicationResponse>> Decide(User user, Guid id, FinalDecision outcome, string? reason = null) =>
        Applications(user, c => c.Decide(id, new RecordDecisionRequest { Outcome = outcome, Reason = reason }, CancellationToken.None));

    private async Task<T> Work<T>(User user, Func<WorkController, Task<T>> action)
    {
        await using var scope = Scope(user);
        return await action(new WorkController(scope.Db, clock) { ControllerContext = TestServices.SignedIn(user) });
    }

    [Fact]
    public async Task My_work_sorts_the_officers_applications_into_queues()
    {
        var client = await NewClient("Ana Petrović", database.Officer);
        var waiting = await Submit(database.Officer, client, new MlScore(0.1, TestData.ModelId));
        var noScore = await Submit(database.Officer, client, new MlFailure(MlErrorCodes.Timeout, null, "x", []));
        var large = await Submit(database.Officer, client, new MlScore(0.1, TestData.ModelId), loanAmount: 30_000);
        var decided = await Submit(database.Officer, client, new MlScore(0.1, TestData.ModelId));
        await Decide(database.Officer, large.Id, FinalDecision.Approve);
        await Decide(database.Officer, decided.Id, FinalDecision.Approve);
        await using (var scope = Scope(database.Officer))
        {
            await new DraftsController(scope.Db, clock) { ControllerContext = TestServices.SignedIn(database.Officer) }
                .Create(JsonDocument.Parse($$"""{ "clientId": "{{client.Id}}" }""").RootElement.Clone(), CancellationToken.None);
        }
        var colleagueClient = await NewClient("Marko Ilić", database.Manager);
        await Submit(database.Manager, colleagueClient, new MlScore(0.1, TestData.ModelId));

        var mine = await Work(database.Officer, c => c.Mine(CancellationToken.None));

        Assert.Equal(new MyWorkCounts(1, 1, 1, 1, 1), mine.Counts);
        Assert.Equal([waiting.Id], mine.AwaitingDecision.Select(a => a.Id));
        Assert.Equal([noScore.Id], mine.ScoringUnavailable.Select(a => a.Id));
        Assert.Equal([large.Id], mine.AwaitingSignOff.Select(a => a.Id));
        Assert.Equal([decided.Id], mine.RecentlyDecided.Select(a => a.Id));
        Assert.Equal([client.Id], mine.Drafts.Select(d => d.ClientId));
    }

    [Fact]
    public async Task Decisions_older_than_a_week_leave_my_work()
    {
        var client = await NewClient("Ana Petrović", database.Officer);
        var decided = await Submit(database.Officer, client, new MlScore(0.1, TestData.ModelId));
        await Decide(database.Officer, decided.Id, FinalDecision.Approve);
        clock.Now = TestData.Now.AddDays(8);

        var mine = await Work(database.Officer, c => c.Mine(CancellationToken.None));

        Assert.Equal(0, mine.Counts.DecidedLast7Days);
        Assert.Empty(mine.RecentlyDecided);
    }

    [Fact]
    public async Task Team_work_shows_the_branch_queue_by_officer()
    {
        var client = await NewClient("Ana Petrović", database.Officer);
        await Submit(database.Officer, client, new MlScore(0.1, TestData.ModelId));
        var large = await Submit(database.Officer, client, new MlScore(0.1, TestData.ModelId), loanAmount: 30_000);
        await Decide(database.Officer, large.Id, FinalDecision.Approve);
        var otherClient = await NewClient("Marko Ilić", database.OtherOfficer);
        await Submit(database.OtherOfficer, otherClient, new MlScore(0.1, TestData.ModelId));

        var team = await Work(database.Manager, c => c.Team(CancellationToken.None));

        Assert.Equal(new TeamWorkCounts(0, 1, 0), team.Counts);
        Assert.Equal([large.Id], team.AwaitingSignOff.Select(a => a.Id));
        var olga = team.Officers.Single(o => o.Id == database.Officer.Id);
        Assert.Equal(("Olga Officer", UserRole.Officer, 1, 1), (olga.DisplayName, olga.Role, olga.Open, olga.AwaitingSignOff));
        Assert.Contains(team.Officers, o => o.Id == database.Manager.Id);
        Assert.DoesNotContain(team.Officers, o => o.Id == database.OtherOfficer.Id);
    }

    private async Task<SearchResponse> Search(User user, string q)
    {
        await using var scope = Scope(user);
        return await new SearchController(scope.Db) { ControllerContext = TestServices.SignedIn(user) }.Get(new SearchQuery { Q = q }, CancellationToken.None);
    }

    [Fact]
    public async Task Search_finds_clients_and_applications_by_number_name_and_reference_within_the_branch()
    {
        var ana = await NewClient("Ana Petrović", database.Officer);
        var anica = await NewClient("Anica Petrović", database.Officer);
        var application = await Submit(database.Officer, ana, new MlScore(0.1, TestData.ModelId));
        var hidden = await NewClient("Ana Petrović Druga", database.OtherOfficer);

        var byName = await Search(database.Officer, "petrov");
        var byNumber = await Search(database.Officer, (await Applications(database.Officer, c => c.Get(application.Id, CancellationToken.None))).Value!.ClientNumber.ToLowerInvariant());
        var byReference = await Search(database.Officer, application.Reference);

        Assert.Equal([ana.Id, anica.Id], byName.Clients.Select(c => c.Id));
        Assert.Equal([application.Id], byName.Applications.Select(a => a.Id));
        Assert.Equal([ana.Id], byNumber.Clients.Select(c => c.Id));
        Assert.Equal([application.Id], byReference.Applications.Select(a => a.Id));
        Assert.DoesNotContain(hidden.Id, byName.Clients.Select(c => c.Id));
    }

    [Fact]
    public async Task An_exact_number_comes_before_partial_matches()
    {
        var clients = new List<Client>();
        for (var i = 0; i < 3; i++)
            clients.Add(await NewClient($"Client {i}", database.Officer));
        var target = clients[0];

        var found = await Search(database.Officer, target.ClientNumber);

        Assert.Equal(target.Id, found.Clients.First().Id);
    }
}
