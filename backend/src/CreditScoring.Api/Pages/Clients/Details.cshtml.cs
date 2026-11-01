using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages.Clients;

public class DetailsModel(ApplicationsApi api, IAuthorizationService authorization) : PageModel
{
    public ClientDetail Client { get; private set; } = null!;

    public bool CanDecide { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await api.GetClientAsync(id, cancellationToken) is not { } client)
            return NotFound();

        Client = client;
        CanDecide = (await authorization.AuthorizeAsync(User, Policies.CanDecide)).Succeeded;
        return Page();
    }
}
