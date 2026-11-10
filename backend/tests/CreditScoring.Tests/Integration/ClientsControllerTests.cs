using System.Text.RegularExpressions;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class ClientsControllerTests(DatabaseFixture database) : IAsyncLifetime
{
    private readonly ManualTimeProvider clock = new(TestData.Now);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static ControllerContext SignedIn(DatabaseFixture database) => TestServices.SignedIn(database.Officer);

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

    private async Task Apply(Guid clientId, DateTimeOffset at, double pBad, User? by = null)
    {
        await using var db = database.CreateContext();
        clock.Now = at;
        var ml = new FakeMlClient { Result = new MlScore(pBad, TestData.ModelId) };
        var controller = new ApplicationsController(db, TestServices.DecisionService(ml, clock), clock)
        {
            ControllerContext = by is null ? SignedIn(database) : TestServices.SignedIn(by),
        };
        var response = await controller.Create(TestData.ValidRequest() with { ClientId = clientId }, CancellationToken.None);
        var created = Assert.IsType<ApplicationResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
        await controller.Decide(created.Id, new RecordDecisionRequest { Outcome = FinalDecision.Reject, Reason = "closed so the client can apply again" }, CancellationToken.None);
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
        Assert.Equal(["in_the_future"], TestServices.FieldCodes(problem, "dateOfBirth"));
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

    private async Task<T> As<T>(User user, Func<ClientsController, Task<T>> action)
    {
        await using var db = database.CreateContext(user.AsActor(), Credentials.BranchScopeOf(Credentials.PrincipalFor(user)));
        return await action(new ClientsController(db, clock) { ControllerContext = TestServices.SignedIn(user) });
    }

    private async Task<User> NewUser(UserRole role)
    {
        var user = User.Create($"u-{Guid.NewGuid():N}"[..20], role.ToString(), role, TestData.Now);
        await using var db = database.CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<ClientResponse> CreateAs(User user, string name)
    {
        var response = await As(user, c => c.Create(new CreateClientRequest { FullName = name }, CancellationToken.None));
        return Assert.IsType<ClientResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
    }

    private static UpdateClientRequest EditOf(ClientResponse client) => new()
    {
        FullName = client.FullName,
        DateOfBirth = client.DateOfBirth,
        Address = client.Address,
        Phone = client.Phone,
        Email = client.Email,
        Employer = client.Employer,
        Version = client.Version,
    };

    private static ProblemDetails Problem(IActionResult result, int status, string code)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal(code, problem.Extensions["code"]);
        return problem;
    }

    [Fact]
    public async Task A_new_client_gets_the_creators_branch_and_contact_data()
    {
        var response = await As(database.Officer, c => c.Create(new CreateClientRequest
        {
            FullName = "Ana Petrović",
            Address = "Bulevar oslobođenja 1",
            Phone = "+381 21 123 456",
            Email = "ana@mail.rs",
            Employer = "NIS",
        }, CancellationToken.None));

        var client = Assert.IsType<ClientResponse>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
        Assert.Equal(database.HeadOfficeId, client.BranchId);
        Assert.Equal(("Bulevar oslobođenja 1", "+381 21 123 456", "ana@mail.rs", "NIS"), (client.Address, client.Phone, client.Email, client.Employer));
        Assert.True(client.IsActive);
        Assert.Null(client.AnonymisedAt);
    }

    [Fact]
    public async Task Officers_see_only_their_branch_and_risk_sees_all()
    {
        var mine = await CreateAs(database.Officer, "Ana Petrović");
        var theirs = await CreateAs(database.OtherOfficer, "Marko Ilić");
        var risk = await NewUser(UserRole.Risk);

        var mineListed = (await As(database.Officer, c => c.List(new ClientQuery(), CancellationToken.None))).Value!.Items;
        var allListed = (await As(risk, c => c.List(new ClientQuery(), CancellationToken.None))).Value!.Items;

        Assert.Equal([mine.Id], mineListed.Select(i => i.Id));
        Assert.Equal(database.HeadOfficeId, mineListed.Single().BranchId);
        Assert.Equal([mine.Id, theirs.Id], allListed.Select(i => i.Id));
        Assert.IsType<NotFoundResult>((await As(database.Officer, c => c.Get(theirs.Id, CancellationToken.None))).Result);
        Assert.Equal(theirs.Id, (await As(risk, c => c.Get(theirs.Id, CancellationToken.None))).Value!.Id);
    }

    [Fact]
    public async Task Edit_replaces_the_details_within_the_branch()
    {
        var mine = await CreateAs(database.Officer, "Ana Petrović");
        var theirs = await CreateAs(database.OtherOfficer, "Marko Ilić");

        var response = await As(database.Officer, c => c.Update(mine.Id, EditOf(mine) with { FullName = "Ana Marić", Phone = "060 111 222" }, CancellationToken.None));
        var outside = await As(database.Officer, c => c.Update(theirs.Id, EditOf(theirs), CancellationToken.None));

        var body = Assert.IsType<ClientResponse>(response.Value);
        Assert.Equal(("Ana Marić", "060 111 222", mine.ClientNumber), (body.FullName, body.Phone, body.ClientNumber));
        Problem(outside.Result!, 404, "not_found");
    }

    [Fact]
    public async Task A_future_birth_date_is_refused_on_edit()
    {
        var mine = await CreateAs(database.Officer, "Ana Petrović");

        var response = await As(database.Officer, c => c.Update(mine.Id, EditOf(mine) with { DateOfBirth = new DateOnly(2030, 1, 1) }, CancellationToken.None));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(response.Result).Value);
        Assert.Equal(["in_the_future"], TestServices.FieldCodes(problem, "dateOfBirth"));
    }

    [Fact]
    public async Task An_inactive_client_gets_no_new_applications_until_reactivated()
    {
        var mine = await CreateAs(database.Officer, "Ana Petrović");

        var off = Assert.IsType<ClientResponse>((await As(database.Officer, c => c.Deactivate(mine.Id, CancellationToken.None))).Value);
        await using (var db = database.CreateContext(database.Officer.AsActor(), database.HeadOfficeId))
        {
            var controller = new ApplicationsController(db, TestServices.DecisionService(new FakeMlClient(), clock), clock)
            {
                ControllerContext = TestServices.SignedIn(database.Officer),
            };
            var refused = await controller.Create(TestData.ValidRequest() with { ClientId = mine.Id }, CancellationToken.None);
            var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(refused.Result).Value);
            Assert.Equal(["client_inactive"], TestServices.FieldCodes(problem, "clientId"));
        }
        var on = Assert.IsType<ClientResponse>((await As(database.Officer, c => c.Reactivate(mine.Id, CancellationToken.None))).Value);

        Assert.False(off.IsActive);
        Assert.True(on.IsActive);
        await Apply(mine.Id, TestData.Now, 0.1);
    }

    [Fact]
    public async Task The_list_filters_by_active()
    {
        var active = await CreateAs(database.Officer, "Ana Petrović");
        var inactive = await CreateAs(database.Officer, "Marko Ilić");
        await As(database.Officer, c => c.Deactivate(inactive.Id, CancellationToken.None));

        var onlyActive = (await As(database.Officer, c => c.List(new ClientQuery { Active = true }, CancellationToken.None))).Value!.Items;
        var onlyInactive = (await As(database.Officer, c => c.List(new ClientQuery { Active = false }, CancellationToken.None))).Value!.Items;

        Assert.Equal([active.Id], onlyActive.Select(i => i.Id));
        Assert.Equal([inactive.Id], onlyInactive.Select(i => i.Id));
        Assert.False(onlyInactive.Single().IsActive);
    }

    [Fact]
    public async Task Anonymising_removes_personal_data_from_the_client_and_its_applications()
    {
        var theirs = await CreateAs(database.OtherOfficer, "Marko Ilić");
        await As(database.OtherOfficer, c => c.Update(theirs.Id, EditOf(theirs) with { Phone = "060 1", DateOfBirth = new DateOnly(1980, 1, 1) }, CancellationToken.None));
        await Apply(theirs.Id, TestData.Now, 0.2, database.OtherOfficer);
        var admin = await NewUser(UserRole.Admin);

        var response = await As(admin, c => c.Anonymise(theirs.Id, CancellationToken.None));

        var body = Assert.IsType<ClientResponse>(response.Value);
        Assert.Equal((Client.AnonymisedName, null, null, false), (body.FullName, body.DateOfBirth, body.Phone, body.IsActive));
        Assert.NotNull(body.AnonymisedAt);
        Assert.Equal(theirs.ClientNumber, body.ClientNumber);
        await using var db = database.CreateContext();
        var applications = await db.Applications.Where(a => a.ClientId == theirs.Id).ToListAsync();
        Assert.NotEmpty(applications);
        Assert.All(applications, a => Assert.Equal(Client.AnonymisedName, a.ApplicantName));
    }

    [Fact]
    public async Task An_anonymised_client_stays_anonymised()
    {
        var mine = await CreateAs(database.Officer, "Ana Petrović");
        var admin = await NewUser(UserRole.Admin);
        await As(admin, c => c.Anonymise(mine.Id, CancellationToken.None));

        Problem((await As(admin, c => c.Anonymise(mine.Id, CancellationToken.None))).Result!, 409, "client_anonymised");
        var current = (await As(database.Officer, c => c.Get(mine.Id, CancellationToken.None))).Value!.Version;
        Problem((await As(database.Officer, c => c.Update(mine.Id, EditOf(mine) with { Version = current }, CancellationToken.None))).Result!, 409, "client_anonymised");
        Problem((await As(database.Officer, c => c.Reactivate(mine.Id, CancellationToken.None))).Result!, 409, "client_anonymised");
    }

    [Fact]
    public async Task Anonymising_an_unknown_client_is_not_found()
    {
        var admin = await NewUser(UserRole.Admin);

        Problem((await As(admin, c => c.Anonymise(Guid.NewGuid(), CancellationToken.None))).Result!, 404, "not_found");
    }
}
