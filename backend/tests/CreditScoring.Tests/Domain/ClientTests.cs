using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class ClientTests
{
    [Fact]
    public void Client_keeps_a_trimmed_name_and_who_created_it()
    {
        var client = Client.Create("  Milica Stojanović ", new DateOnly(1984, 3, 9), TestData.Officer, TestData.Now);

        Assert.Equal("Milica Stojanović", client.FullName);
        Assert.Equal(new DateOnly(1984, 3, 9), client.DateOfBirth);
        Assert.Equal(TestData.Officer.UserId, client.CreatedByUserId);
        Assert.Equal(TestData.Now, client.CreatedAt);
        Assert.NotEqual(Guid.Empty, client.Id);
    }

    [Fact]
    public void Date_of_birth_is_optional()
    {
        Assert.Null(Client.Create("Ana", null, TestData.Officer, TestData.Now).DateOfBirth);
    }

    [Fact]
    public void Blank_name_and_future_birth_date_are_impossible()
    {
        Assert.ThrowsAny<ArgumentException>(() => Client.Create(" ", null, TestData.Officer, TestData.Now));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Client.Create("Ana", DateOnly.FromDateTime(TestData.Now.UtcDateTime).AddDays(1), TestData.Officer, TestData.Now));
    }

    [Theory]
    [InlineData("1990-11-01", 36)]
    [InlineData("1990-11-02", 35)]
    public void Age_is_counted_in_whole_years_on_a_date(string birth, int expected)
    {
        var client = Client.Create("Ana", DateOnly.Parse(birth), TestData.Officer, TestData.Now);

        Assert.Equal(expected, client.AgeOn(new DateOnly(2026, 11, 1)));
    }
}
