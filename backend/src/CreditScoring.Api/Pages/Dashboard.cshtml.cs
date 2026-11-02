using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Reporting;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages;

public class DashboardModel(ApplicationsApi api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? To { get; set; }

    public DashboardStats Stats { get; private set; } = null!;

    public ScoringModelResponse? Active { get; private set; }

    public Dictionary<string, string?> HistoryRoute(HistoryOutcome? outcome = null, ReviewFilter? review = null) => new()
    {
        ["from"] = From?.ToString("yyyy-MM-dd"),
        ["to"] = To?.ToString("yyyy-MM-dd"),
        ["outcome"] = outcome?.ToString(),
        ["review"] = review?.ToString(),
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var zone = TimeZoneInfo.Local;
        var query = new StatsQuery
        {
            From = From is { } from ? HistoryFilter.StartOfDay(from, zone) : null,
            To = To is { } to ? HistoryFilter.StartOfDay(to.AddDays(1), zone) : null,
        };
        Stats = await api.GetStatsAsync(query, cancellationToken);
        Active = (await api.ListModelsAsync(cancellationToken)).FirstOrDefault(m => m.Role == ModelRole.Active);
    }
}
