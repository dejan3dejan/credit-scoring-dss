using System.Diagnostics;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages.Applications;

public class NewModel(ApplicationsApi api) : PageModel
{
    [BindProperty]
    public CreateApplicationRequest Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        switch (await api.SubmitAsync(Input, cancellationToken))
        {
            case Submitted submitted:
                return RedirectToPage("Details", new { id = submitted.Application.Id });
            case Invalid invalid:
                FormErrors.Apply<CreateApplicationRequest>(ModelState, nameof(Input), invalid.Errors);
                return Page();
            default:
                throw new UnreachableException();
        }
    }
}
