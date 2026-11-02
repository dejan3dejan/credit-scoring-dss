using System.Diagnostics;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages.Applications;

public class DetailsModel(ApplicationsApi api, IAuthorizationService authorization) : PageModel
{
    public ApplicationResponse Application { get; private set; } = null!;

    [BindProperty]
    public RecordDecisionRequest Review { get; set; } = new();

    public bool AlreadyDecidedElsewhere { get; private set; }

    public bool CanDecide { get; private set; }

    public string? RescoreMessage { get; private set; }

    public List<ShadowScoreResponse>? ShadowScores { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken))
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!(await authorization.AuthorizeAsync(User, Policies.CanDecide)).Succeeded)
            return Forbid();

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

    public async Task<IActionResult> OnPostRescoreAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!(await authorization.AuthorizeAsync(User, Policies.CanDecide)).Succeeded)
            return Forbid();

        switch (await api.RescoreAsync(id, cancellationToken))
        {
            case null:
                return NotFound();
            case Submitted:
                return RedirectToPage(new { id });
            case Conflicted conflicted:
                RescoreMessage = conflicted.Message;
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
        CanDecide = (await authorization.AuthorizeAsync(User, Policies.CanDecide)).Succeeded;
        if ((await authorization.AuthorizeAsync(User, Policies.CanSeeStats)).Succeeded)
            ShadowScores = await api.GetShadowScoresAsync(id, cancellationToken);
        return true;
    }
}
