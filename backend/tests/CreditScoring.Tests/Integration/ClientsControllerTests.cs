using System.Text.RegularExpressions;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class ClientsControllerTests(DatabaseFixture database) : IAsyncLifetime
{
    private readonly ManualTimeProvider clock = new(TestData.Now);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static ControllerContext SignedIn(DatabaseFixture database) => new()
    {
        HttpContext = new DefaultHttpContext { User = Credentials.PrincipalFor(database.Officer) },
    };

    private async Task<T> WithClients<T>(Func<ClientsController, Task<T>> action)
    {
        await using var db = database.CreateContext();
        return await action(new ClientsController(db, clock) { ControllerContext = SignedIn(database) });
    }

    private async Task<ClientResponse> Create(string name, DateOnly? birth = null)
    {
        var response = await WithClients(c => c.Create(new CreateClientRequest { FullName = name, DateOfBirth = birth }, CancellationToken.None));
        return Assert.IsType<ClientResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
    }

    private async Task Apply(Guid clientId, DateTimeOffset at, double pBad)
    {
        await using var db = database.CreateContext();
        clock.Now = at;
        var ml = new FakeMlClient { Result = new MlScore(pBad, TestData.ModelId) };
        var controller = new ApplicationsController(db, TestServices.DecisionService(ml, clock), clock) { ControllerContext = SignedIn(database) };
        var response = await controller.Create(TestData.ValidRequest() with { ClientId = clientId }, CancellationToken.None);
        Assert.IsType<CreatedAtActionResult>(response.Result);
    }

    [Fact]
    public async Task New_clients_get_increasing_client_numbers()
    {
        var first = await Create("Ana Petrović", new DateOnly(1985, 5, 1));
        var second = await Create("Marko Ilić");

        Assert.Matches(new Regex("^C-[0-9]{6}$"), first.ClientNumber);
        Assert.True(string.CompareOrdinal(second.ClientNumber, first.ClientNumber) > 0);
        Assert.Equal("Ana Petrović", first.FullName);
        Assert.Equal(new DateOnly(1985, 5, 1), first.DateOfBirth);
    }

    [Fact]
    public async Task Future_birth_date_is_a_validation_problem()
    {
        var response = await WithClients(c => c.Create(
            new CreateClientRequest { FullName = "Ana", DateOfBirth = new DateOnly(2030, 1, 1) }, CancellationToken.None));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
        Assert.Equal(["dateOfBirth"], problem.Errors.Keys);
    }

    [Fact]
    public async Task Search_matches_name_or_client_number_and_treats_wildcards_literally()
    {
        var ana = await Create("Ana Petrović");
        await Create("Marko Ilić");
        await Create("100% Sure");

        async Task<List<string>> Names(string search) =>
            (await WithClients(c => c.List(new ClientQuery { Search = search }, CancellationToken.None))).Value!
                .Items.Select(i => i.FullName).ToList();

        Assert.Equal(["Ana Petrović"], await Names(" petro "));
        Assert.Equal(["Ana Petrović"], await Names(ana.ClientNumber));
        Assert.Equal(["100% Sure"], await Names("100%"));
        Assert.Equal(["100% Sure", "Ana Petrović", "Marko Ilić"], await Names(""));
    }

    [Fact]
    public async Task List_counts_applications_and_shows_the_latest()
    {
        var ana = await Create("Ana Petrović");
        await Apply(ana.Id, TestData.Now, 0.1);
        await Apply(ana.Id, TestData.Now.AddDays(3), 0.3);

        var item = (await WithClients(c => c.List(new ClientQuery(), CancellationToken.None))).Value!.Items.Single();

        Assert.Equal(2, item.ApplicationCount);
        Assert.Equal(TestData.Now.AddDays(3), item.LastApplicationAt);
    }

    [Fact]
    public async Task Profile_lists_the_clients_applications_newest_first()
    {
        var ana = await Create("Ana Petrović");
        var other = await Create("Marko Ilić");
        await Apply(ana.Id, TestData.Now, 0.1);
        await Apply(other.Id, TestData.Now.AddDays(1), 0.1);
        await Apply(ana.Id, TestData.Now.AddDays(2), 0.3);

        var profile = (await WithClients(c => c.Get(ana.Id, CancellationToken.None))).Value!;

        Assert.Equal("Ana Petrović", profile.FullName);
        Assert.Equal([TestData.Now.AddDays(2), TestData.Now], profile.Applications.Select(a => a.SubmittedAt));
        Assert.All(profile.Applications, a => Assert.Equal(ana.Id, a.ClientId));
    }

    [Fact]
    public async Task Unknown_client_is_not_found()
    {
        Assert.IsType<NotFoundResult>((await WithClients(c => c.Get(Guid.NewGuid(), CancellationToken.None))).Result);
    }
}
