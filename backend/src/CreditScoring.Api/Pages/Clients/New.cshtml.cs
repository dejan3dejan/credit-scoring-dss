using System.Diagnostics;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages.Clients;

public class NewModel(ApplicationsApi api) : PageModel
{
    [BindProperty]
    public CreateClientRequest Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var formatErrors = FormErrors.FormatErrors<CreateClientRequest>(ModelState, nameof(Input));
        ModelState.Clear();
        switch (await api.CreateClientAsync(Input, cancellationToken))
        {
            case ClientCreated created:
                return RedirectToPage("/Applications/New", new { clientId = created.Client.Id });
            case Invalid invalid:
                FormErrors.Apply<CreateClientRequest>(ModelState, nameof(Input), invalid.Errors, formatErrors);
                return Page();
            default:
                throw new UnreachableException();
        }
    }
}
