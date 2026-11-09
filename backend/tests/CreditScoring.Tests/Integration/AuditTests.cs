using System.Text.Json;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Controllers;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class AuditTests(DatabaseFixture database) : IAsyncLifetime
{
    private User admin = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        admin = User.Create($"a-{Guid.NewGuid():N}"[..20], "Vesna Admin", UserRole.Admin, TestData.Now);
        admin.SetPasswordHash(Credentials.Hash(admin, "admin password 1"));
        await using var db = database.CreateContext();
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        await db.AuditEntries.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<List<AuditEntry>> Entries()
    {
        await using var db = database.CreateContext();
        return await db.AuditEntries.AsNoTracking().OrderBy(e => e.At).ThenBy(e => e.Id).ToListAsync();
    }

    private static JsonElement Changes(AuditEntry entry) => JsonDocument.Parse(entry.Changes).RootElement;

    private async Task<Client> NewClient()
    {
        await using var db = database.CreateContext(database.Officer.AsActor());
        var client = Client.Create("Ana Petrović", new DateOnly(1990, 5, 1), database.HeadOfficeId, database.Officer.AsActor(), TestData.Now);
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client;
    }

    [Fact]
    public void Audit_entries_have_a_fixed_shape_in_the_database_model()
    {
        using var db = database.CreateContext();
        var entity = db.Model.FindEntityType(typeof(AuditEntry))!;

        Assert.Equal("audit_entries", entity.GetTableName());
        Assert.Equal("jsonb", entity.FindProperty(nameof(AuditEntry.Changes))!.GetColumnType());
    }

    [Fact]
    public async Task A_created_row_is_recorded_with_who_when_and_its_values()
    {
        var client = await NewClient();

        var entry = Assert.Single(await Entries());
        Assert.Equal((AuditAction.Created, "Client", client.Id.ToString()), (entry.Action, entry.Entity, entry.EntityId));
        Assert.Equal((database.Officer.Id, "Olga Officer"), (entry.UserId!.Value, entry.UserName));
        Assert.Equal(DatabaseFixture.TraceId, entry.TraceId);
        var changes = Changes(entry);
        Assert.Equal("[personal]", changes.GetProperty("fullName").GetProperty("after").GetString());
        Assert.Equal("[personal]", changes.GetProperty("dateOfBirth").GetProperty("after").GetString());
        Assert.Equal(database.HeadOfficeId.ToString(), changes.GetProperty("branchId").GetProperty("after").GetString());
        Assert.False(changes.GetProperty("fullName").TryGetProperty("before", out _));
        Assert.False(changes.TryGetProperty("clientNumber", out _));
    }

    [Fact]
    public async Task An_update_records_only_what_changed_before_and_after()
    {
        var user = User.Create($"u-{Guid.NewGuid():N}"[..20], "Ana", UserRole.Officer, TestData.Now, database.HeadOfficeId);
        await using (var db = database.CreateContext())
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        await using (var db = database.CreateContext(admin.AsActor()))
        {
            await db.AuditEntries.ExecuteDeleteAsync();
            var tracked = await db.Users.SingleAsync(u => u.Id == user.Id);
            tracked.Edit("Ana", null, UserRole.Manager, Languages.English, database.HeadOfficeId);
            await db.SaveChangesAsync();
        }

        var entry = Assert.Single(await Entries());
        Assert.Equal((AuditAction.Updated, "User", admin.Id), (entry.Action, entry.Entity, entry.UserId!.Value));
        var changes = Changes(entry);
        Assert.Equal(["role"], changes.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("Officer", "Manager"),
            (changes.GetProperty("role").GetProperty("before").GetString(), changes.GetProperty("role").GetProperty("after").GetString()));
    }

    [Fact]
    public async Task Secrets_are_redacted()
    {
        await using (var db = database.CreateContext(admin.AsActor()))
        {
            var tracked = await db.Users.SingleAsync(u => u.Id == admin.Id);
            tracked.ResetPassword(Credentials.Hash(tracked, "another password 1"));
            await db.SaveChangesAsync();
        }

        var changes = Changes(Assert.Single(await Entries()));
        foreach (var secret in new[] { "passwordHash", "securityStamp" })
        {
            Assert.Equal("[redacted]", changes.GetProperty(secret).GetProperty("before").GetString());
            Assert.Equal("[redacted]", changes.GetProperty(secret).GetProperty("after").GetString());
        }
        Assert.True(changes.GetProperty("mustChangePassword").GetProperty("after").GetBoolean());
    }

    [Fact]
    public async Task A_deleted_row_is_recorded_with_its_last_values()
    {
        var client = await NewClient();
        await using (var db = database.CreateContext(admin.AsActor()))
        {
            db.Clients.Remove(await db.Clients.SingleAsync(c => c.Id == client.Id));
            await db.SaveChangesAsync();
        }

        var deleted = (await Entries()).Last();
        Assert.Equal(AuditAction.Deleted, deleted.Action);
        var changes = Changes(deleted);
        Assert.Equal("[personal]", changes.GetProperty("fullName").GetProperty("before").GetString());
        Assert.False(changes.GetProperty("fullName").TryGetProperty("after", out _));
    }

    [Fact]
    public async Task Writes_without_a_signed_in_user_have_no_user()
    {
        await using (var db = database.CreateContext())
        {
            var tracked = await db.Users.SingleAsync(u => u.Id == admin.Id);
            tracked.RecordFailedSignIn(TestData.Now);
            await db.SaveChangesAsync();
        }

        var entry = Assert.Single(await Entries());
        Assert.Null(entry.UserId);
        Assert.Null(entry.UserName);
    }

    [Fact]
    public async Task Saving_nothing_writes_nothing()
    {
        await using (var db = database.CreateContext(admin.AsActor()))
        {
            await db.Users.SingleAsync(u => u.Id == admin.Id);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await Entries());
    }

    private async Task<PagedResult<AuditEntryResponse>> Query(AuditQuery query)
    {
        await using var db = database.CreateContext();
        return await new AuditController(db) { ControllerContext = TestServices.SignedIn(admin) }.List(query, CancellationToken.None);
    }

    [Fact]
    public async Task The_audit_log_lists_newest_first_with_the_changes_as_json()
    {
        var first = await NewClient();
        var second = await NewClient();

        var page = await Query(new AuditQuery());

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([second.Id.ToString(), first.Id.ToString()], page.Items.Select(i => i.EntityId));
        var item = page.Items[0];
        Assert.Equal(("Client", AuditAction.Created, "Olga Officer"), (item.Entity, item.Action, item.UserName));
        Assert.Equal("[personal]", item.Changes.GetProperty("fullName").GetProperty("after").GetString());
    }

    [Fact]
    public async Task The_audit_log_filters_by_entity_user_action_and_time()
    {
        var client = await NewClient();
        await using (var db = database.CreateContext(admin.AsActor()))
        {
            (await db.Users.SingleAsync(u => u.Id == admin.Id)).SetPreferredLanguage(Languages.Serbian);
            await db.SaveChangesAsync();
        }
        var all = await Query(new AuditQuery());
        var userEntry = all.Items.Single(i => i.Entity == "User");

        Assert.Equal([client.Id.ToString()], (await Query(new AuditQuery { Entity = "Client" })).Items.Select(i => i.EntityId));
        Assert.Equal([admin.Id.ToString()], (await Query(new AuditQuery { UserId = admin.Id })).Items.Select(i => i.EntityId));
        Assert.Equal(["User"], (await Query(new AuditQuery { Action = AuditAction.Updated })).Items.Select(i => i.Entity));
        Assert.Equal(["Client"], (await Query(new AuditQuery { EntityId = client.Id.ToString() })).Items.Select(i => i.Entity));
        Assert.Equal(["User"], (await Query(new AuditQuery { From = userEntry.At })).Items.Select(i => i.Entity));
        Assert.Equal(["Client"], (await Query(new AuditQuery { To = userEntry.At })).Items.Select(i => i.Entity));
    }

    [Fact]
    public async Task The_audit_log_pages()
    {
        for (var i = 0; i < 3; i++)
            await NewClient();

        var page = await Query(new AuditQuery { Page = 2, PageSize = 2 });

        Assert.Equal((3, 2, 2), (page.TotalCount, page.Page, page.PageSize));
        Assert.Single(page.Items);
    }
}
