using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class ClientTests
{
    private static readonly ClientContact Contact = new(" Bulevar oslobođenja 1 ", " +381 21 123 456 ", " ana@mail.rs ", " NIS ");

    private static Client NewClient() =>
        Client.Create("Ana Petrović", new DateOnly(1990, 5, 1), TestData.BranchId, TestData.Officer, TestData.Now, Contact);

    [Fact]
    public void Client_keeps_a_trimmed_name_who_created_it_and_its_branch()
    {
        var client = Client.Create("  Milica Stojanović ", new DateOnly(1984, 3, 9), TestData.BranchId, TestData.Officer, TestData.Now);

        Assert.Equal("Milica Stojanović", client.FullName);
        Assert.Equal(new DateOnly(1984, 3, 9), client.DateOfBirth);
        Assert.Equal(TestData.Officer.UserId, client.CreatedByUserId);
        Assert.Equal(TestData.BranchId, client.BranchId);
        Assert.Equal(TestData.Now, client.CreatedAt);
        Assert.NotEqual(Guid.Empty, client.Id);
        Assert.True(client.IsActive);
        Assert.Null(client.AnonymisedAt);
        Assert.Equal((null, null, null, null), (client.Address, client.Phone, client.Email, client.Employer));
    }

    [Fact]
    public void Contact_data_is_trimmed_and_blank_values_are_none()
    {
        var client = NewClient();
        Assert.Equal(("Bulevar oslobođenja 1", "+381 21 123 456", "ana@mail.rs", "NIS"), (client.Address, client.Phone, client.Email, client.Employer));

        client.Edit("Ana Petrović", null, new ClientContact(" ", "", null, "  "), TestData.Now);
        Assert.Equal((null, null, null, null), (client.Address, client.Phone, client.Email, client.Employer));
    }

    [Fact]
    public void Date_of_birth_is_optional()
    {
        Assert.Null(Client.Create("Ana", null, TestData.BranchId, TestData.Officer, TestData.Now).DateOfBirth);
    }

    [Fact]
    public void Blank_name_future_birth_date_and_no_branch_are_impossible()
    {
        var tomorrow = DateOnly.FromDateTime(TestData.Now.UtcDateTime).AddDays(1);

        Assert.ThrowsAny<ArgumentException>(() => Client.Create(" ", null, TestData.BranchId, TestData.Officer, TestData.Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => Client.Create("Ana", tomorrow, TestData.BranchId, TestData.Officer, TestData.Now));
        Assert.ThrowsAny<ArgumentException>(() => Client.Create("Ana", null, Guid.Empty, TestData.Officer, TestData.Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewClient().Edit("Ana", tomorrow, Contact, TestData.Now));
    }

    [Fact]
    public void Editing_replaces_name_birth_date_and_contact()
    {
        var client = NewClient();

        Assert.IsType<Changed>(client.Edit(" Ana Marić ", null, new ClientContact(null, "060 111", null, null), TestData.Now));

        Assert.Equal(("Ana Marić", null, "060 111", null), (client.FullName, client.DateOfBirth, client.Phone, client.Address));
    }

    [Fact]
    public void Deactivating_and_reactivating()
    {
        var client = NewClient();

        Assert.IsType<Changed>(client.Deactivate());
        Assert.False(client.IsActive);
        Assert.IsType<Changed>(client.Deactivate());
        Assert.IsType<Changed>(client.Reactivate());
        Assert.True(client.IsActive);
    }

    [Fact]
    public void Anonymising_removes_personal_data_and_deactivates()
    {
        var client = NewClient();

        Assert.IsType<Changed>(client.Anonymise(TestData.Now));

        Assert.Equal(Client.AnonymisedName, client.FullName);
        Assert.Null(client.DateOfBirth);
        Assert.Equal((null, null, null, null), (client.Address, client.Phone, client.Email, client.Employer));
        Assert.False(client.IsActive);
        Assert.Equal(TestData.Now, client.AnonymisedAt);
        Assert.Equal(TestData.BranchId, client.BranchId);
    }

    [Fact]
    public void An_anonymised_client_cannot_be_edited_reactivated_or_anonymised_again()
    {
        var client = NewClient();
        client.Anonymise(TestData.Now);

        foreach (var result in new[]
                 {
                     client.Edit("Ana", null, Contact, TestData.Now),
                     client.Reactivate(),
                     client.Anonymise(TestData.Now),
                 })
        {
            var refused = Assert.IsType<Refused>(result);
            Assert.Equal((null, "client_anonymised"), (refused.Field, refused.Code));
        }
        Assert.Equal(Client.AnonymisedName, client.FullName);
        Assert.False(client.IsActive);
    }

    [Fact]
    public void Anonymising_a_client_also_removes_the_name_from_its_applications()
    {
        var client = NewClient();
        var applications = new[] { TestData.Application(client), TestData.Application(client) };

        Assert.IsType<Changed>(ClientAnonymisation.Apply(client, applications, TestData.Now));

        Assert.All(applications, a => Assert.Equal(Client.AnonymisedName, a.ApplicantName));
    }

    [Fact]
    public void Anonymisation_refuses_applications_of_another_client()
    {
        var client = NewClient();
        var other = NewClient();

        Assert.Throws<ArgumentException>(() => ClientAnonymisation.Apply(client, [TestData.Application(other)], TestData.Now));
    }
}
