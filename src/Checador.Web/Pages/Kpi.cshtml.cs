using System.ComponentModel.DataAnnotations;
using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages;

[Authorize(Policy = "RestrictedModulesOnly")]
public sealed class KpiModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public KpiModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty(SupportsGet = true)]
    [Display(Name = "Desde")]
    public DateTime From { get; set; } = DateTime.Today.AddDays(-7);

    [BindProperty(SupportsGet = true)]
    [Display(Name = "Hasta")]
    public DateTime To { get; set; } = DateTime.Today;

    public ApiKpiResponse? Kpi { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToLogin();
        }

        if (MustChangePassword)
        {
            return RedirectToPasswordChange();
        }

        if (From > To)
        {
            ErrorMessage = "Rango invalido.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(ApiBasicToken))
        {
            return RedirectToLogin();
        }

        Kpi = await _apiClient.GetKpiAsync(ApiBasicToken, From.Date, To.Date.AddDays(1).AddTicks(-1), cancellationToken);
        if (Kpi is null)
        {
            ErrorMessage = "No se pudo obtener KPI desde la API.";
        }

        return Page();
    }
}
