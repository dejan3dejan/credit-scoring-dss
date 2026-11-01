using System.ComponentModel.DataAnnotations;

namespace CreditScoring.Api.Decisions;

public class DecisionOptions
{
    public const string SectionName = "Decision";

    [Range(0.0, 1.0, MinimumIsExclusive = true, MaximumIsExclusive = true)]
    public double RejectThreshold { get; set; }
}
