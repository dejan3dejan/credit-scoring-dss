using CreditScoring.Api.Decisions;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CreditScoring.Api.Pages;

public class ModelModel(IOptions<MlServiceOptions> ml, IOptions<DecisionOptions> decision) : PageModel
{
    public string ModelId => ml.Value.ExpectedModelId;
    public double Threshold => decision.Value.RejectThreshold;

    public void OnGet()
    {
    }
}
