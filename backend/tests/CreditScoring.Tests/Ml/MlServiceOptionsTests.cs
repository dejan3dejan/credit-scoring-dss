using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Ml;

namespace CreditScoring.Tests.Ml;

public class MlServiceOptionsTests
{
    private static bool IsValid(MlServiceOptions options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true);

    private static MlServiceOptions Valid() => new()
    {
        BaseUrl = new Uri("http://localhost:8000/"),
        TimeoutSeconds = 5,
        ShadowTimeoutSeconds = 2,
    };

    [Fact]
    public void Complete_options_are_valid()
    {
        Assert.True(IsValid(Valid()));
    }

    [Fact]
    public void Missing_base_url_is_invalid()
    {
        var options = Valid();
        options.BaseUrl = null;

        Assert.False(IsValid(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Timeout_outside_range_is_invalid(int seconds)
    {
        var options = Valid();
        options.TimeoutSeconds = seconds;

        Assert.False(IsValid(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Shadow_timeout_outside_range_is_invalid(int seconds)
    {
        var options = Valid();
        options.ShadowTimeoutSeconds = seconds;

        Assert.False(IsValid(options));
    }
}
