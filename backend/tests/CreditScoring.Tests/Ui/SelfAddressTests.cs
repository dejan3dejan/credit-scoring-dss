using CreditScoring.Api.Ui;

namespace CreditScoring.Tests.Ui;

public class SelfAddressTests
{
    [Fact]
    public void Https_is_preferred_so_no_redirect_is_followed()
    {
        Assert.Equal(
            new Uri("https://localhost:7287/"),
            SelfAddress.Choose(["http://localhost:5276", "https://localhost:7287"]));
    }

    [Fact]
    public void Http_is_used_when_it_is_the_only_address()
    {
        Assert.Equal(new Uri("http://localhost:5276/"), SelfAddress.Choose(["http://localhost:5276"]));
    }

    [Theory]
    [InlineData("http://+:8080")]
    [InlineData("http://*:8080")]
    [InlineData("http://0.0.0.0:8080")]
    [InlineData("http://[::]:8080")]
    public void Wildcard_hosts_are_called_through_localhost(string address)
    {
        Assert.Equal(new Uri("http://localhost:8080/"), SelfAddress.Choose([address]));
    }

    [Fact]
    public void No_usable_address_fails_loud()
    {
        Assert.Throws<InvalidOperationException>(() => SelfAddress.Choose([]));
    }
}
