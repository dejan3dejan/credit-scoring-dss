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
        ExpectedModelId = "credit_rf_isotonic_v1",
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

    [Fact]
    public void Blank_expected_model_id_is_invalid()
    {
        var options = Valid();
        options.ExpectedModelId = "";

        Assert.False(IsValid(options));
    }
}
