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

    [BindProperty(SupportsGet = true)]
    public Guid? ClientId { get; set; }

    [BindProperty(SupportsGet = true, Name = "from")]
    public Guid? FromApplication { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public ClientDetail? Client { get; private set; }

    public PagedResult<ClientSummary>? Clients { get; private set; }

    public ApplicationResponse? Prefilled { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (ClientId is not { } clientId)
        {
            Clients = await api.ListClientsAsync(new ClientQuery { Search = Search, PageSize = 8 }, cancellationToken);
            return Page();
        }

        Client = await api.GetClientAsync(clientId, cancellationToken);
        if (Client is null)
            return NotFound();

        if (FromApplication is { } fromId)
        {
            Prefilled = await api.GetAsync(fromId, cancellationToken);
            if (Prefilled is null || Prefilled.ClientId != clientId)
                return NotFound();
            Input = CreateApplicationRequest.From(Prefilled);
        }
        else
        {
            Input = new CreateApplicationRequest { ClientId = clientId };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var formatErrors = FormErrors.FormatErrors<CreateApplicationRequest>(ModelState, nameof(Input));
        ModelState.Clear();
        switch (await api.SubmitAsync(Input, cancellationToken))
        {
            case Submitted submitted:
                return RedirectToPage("Details", new { id = submitted.Application.Id });
            case Invalid invalid:
                FormErrors.Apply<CreateApplicationRequest>(ModelState, nameof(Input), invalid.Errors, formatErrors);
                if (Input.ClientId is { } clientId)
                    Client = await api.GetClientAsync(clientId, cancellationToken);
                return Page();
            default:
                throw new UnreachableException();
        }
    }
}
