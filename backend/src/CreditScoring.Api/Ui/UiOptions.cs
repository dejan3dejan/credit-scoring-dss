using System.ComponentModel.DataAnnotations;

namespace CreditScoring.Api.Ui;

public class UiOptions
{
    public const string SectionName = "Ui";

    [Required]
    public Uri? ApiBaseUrl { get; set; }
}
