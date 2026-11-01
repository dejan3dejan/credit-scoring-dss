using System.Diagnostics;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages.Applications;

public class DetailsModel(ApplicationsApi api) : PageModel
{
    public ApplicationResponse Application { get; private set; } = null!;

    [BindProperty]
    public RecordDecisionRequest Review { get; set; } = new();

    public bool AlreadyDecidedElsewhere { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken))
            return NotFound();

        Review = new RecordDecisionRequest { DecidedBy = Application.SubmittedBy };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        ModelState.Clear();
        switch (await api.DecideAsync(id, Review, cancellationToken))
        {
            case Submitted:
                return RedirectToPage(new { id });
            case Invalid invalid:
                FormErrors.Apply<RecordDecisionRequest>(ModelState, nameof(Review), invalid.Errors);
                break;
            case AlreadyDecided:
                AlreadyDecidedElsewhere = true;
                break;
            default:
                throw new UnreachableException();
        }

        return await LoadAsync(id, cancellationToken) ? Page() : NotFound();
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await api.GetAsync(id, cancellationToken) is not { } application)
            return false;

        Application = application;
        return true;
    }
}
