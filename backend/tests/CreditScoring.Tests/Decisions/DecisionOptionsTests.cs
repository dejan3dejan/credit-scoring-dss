using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Decisions;
using Microsoft.Extensions.Configuration;

namespace CreditScoring.Tests.Decisions;

public class DecisionOptionsTests
{
    private static bool IsValid(double threshold)
    {
        var options = new DecisionOptions { RejectThreshold = threshold };
        return Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true);
    }

    [Fact]
    public void Configured_threshold_string_binds_to_exactly_one_sixth()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Decision:RejectThreshold"] = "0.16666666666666666" })
            .Build();

        var options = configuration.GetSection(DecisionOptions.SectionName).Get<DecisionOptions>()!;

        Assert.Equal(1.0 / 6.0, options.RejectThreshold);
    }

    [Fact]
    public void One_sixth_is_valid()
    {
        Assert.True(IsValid(1.0 / 6.0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-0.1)]
    public void Threshold_outside_open_unit_interval_is_invalid(double threshold)
    {
        Assert.False(IsValid(threshold));
    }
}
