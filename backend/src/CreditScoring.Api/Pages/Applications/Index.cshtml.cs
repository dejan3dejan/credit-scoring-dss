using CreditScoring.Api.Contracts;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages.Applications;

public class IndexModel(ApplicationsApi api) : PageModel
{
    private const int PageSize = 20;

    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public HistoryOutcome? Outcome { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public PagedResult<ApplicationSummary> Result { get; private set; } = null!;

    public bool IsFiltered => !string.IsNullOrWhiteSpace(Name) || From is not null || To is not null || Outcome is not null;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var filter = new HistoryFilter { Name = Name, From = From, To = To, Outcome = Outcome, Page = PageNumber };
        Result = await api.ListAsync(filter.ToQuery(TimeZoneInfo.Local, PageSize), cancellationToken);
    }

    public Dictionary<string, string?> RouteFor(int page) => new()
    {
        ["name"] = Name,
        ["from"] = From?.ToString("yyyy-MM-dd"),
        ["to"] = To?.ToString("yyyy-MM-dd"),
        ["outcome"] = Outcome?.ToString(),
        ["p"] = page.ToString(),
    };
}
