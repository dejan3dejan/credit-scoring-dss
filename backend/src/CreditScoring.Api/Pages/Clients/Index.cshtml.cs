using CreditScoring.Api.Contracts;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages.Clients;

public class IndexModel(ApplicationsApi api) : PageModel
{
    private const int PageSize = 20;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public PagedResult<ClientSummary> Result { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Result = await api.ListClientsAsync(
            new ClientQuery { Search = Search, Page = Math.Max(PageNumber, 1), PageSize = PageSize },
            cancellationToken);
    }
}
