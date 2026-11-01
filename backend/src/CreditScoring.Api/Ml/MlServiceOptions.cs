using System.ComponentModel.DataAnnotations;

namespace CreditScoring.Api.Ml;

public class MlServiceOptions
{
    public const string SectionName = "MlService";

    [Required]
    public Uri? BaseUrl { get; set; }

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; }

    [Required]
    public string ExpectedModelId { get; set; } = "";
}
