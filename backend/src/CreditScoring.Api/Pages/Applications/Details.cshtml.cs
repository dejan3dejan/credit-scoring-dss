using CreditScoring.Api.Contracts;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages.Applications;

public class DetailsModel(ApplicationsApi api) : PageModel
{
    public ApplicationResponse Application { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await api.GetAsync(id, cancellationToken) is not { } application)
            return NotFound();

        Application = application;
        return Page();
    }
}
