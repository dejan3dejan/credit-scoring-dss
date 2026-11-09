using System.Diagnostics;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages;

public class ModelModel(ApplicationsApi api, IAuthorizationService authorization) : PageModel
{
    public List<ScoringModelResponse> Models { get; private set; } = [];

    public ScoringModelResponse? Active => Models.FirstOrDefault(m => m.Role == ModelRole.Active);

    public bool CanManage { get; private set; }

    public List<AvailableModelResponse>? Available { get; private set; }

    public List<ModelEventResponse> Events { get; private set; } = [];

    public string? Conflict { get; private set; }

    [BindProperty]
    public string? ModelId { get; set; }

    [BindProperty]
    public string? Reason { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public Task<IActionResult> OnPostRegisterAsync(CancellationToken cancellationToken) =>
        ChangeAsync(() => api.RegisterModelAsync(ModelId ?? "", cancellationToken), cancellationToken);

    public Task<IActionResult> OnPostPromoteAsync(CancellationToken cancellationToken) =>
        ChangeAsync(() => api.PromoteModelAsync(ModelId ?? "", Reason, cancellationToken), cancellationToken);

    public Task<IActionResult> OnPostRetireAsync(CancellationToken cancellationToken) =>
        ChangeAsync(() => api.RetireModelAsync(ModelId ?? "", Reason, cancellationToken), cancellationToken);

    private async Task<IActionResult> ChangeAsync(Func<Task<SubmitResult>> change, CancellationToken cancellationToken)
    {
        if (!(await authorization.AuthorizeAsync(User, Permissions.ModelsManage)).Succeeded)
            return Forbid();

        ModelState.Clear();
        switch (await change())
        {
            case Done:
                return RedirectToPage();
            case Invalid invalid:
                foreach (var (key, messages) in invalid.Errors)
                {
                    foreach (var message in messages)
                        ModelState.AddModelError(key.Equals("reason", StringComparison.OrdinalIgnoreCase) ? nameof(Reason) : string.Empty, message);
                }
                break;
            case Conflicted conflicted:
                Conflict = conflicted.Message;
                break;
            default:
                throw new UnreachableException();
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Models = await api.ListModelsAsync(cancellationToken);
        CanManage = (await authorization.AuthorizeAsync(User, Permissions.ModelsManage)).Succeeded;
        if (CanManage)
        {
            Available = await api.ListAvailableModelsAsync(cancellationToken);
            Events = await api.ListModelEventsAsync(cancellationToken);
        }
    }
}
