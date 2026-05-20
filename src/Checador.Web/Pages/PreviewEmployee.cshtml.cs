using System.ComponentModel.DataAnnotations;
using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages;

public sealed class PreviewEmployeeModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public PreviewEmployeeModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty(SupportsGet = true)]
    [Display(Name = "User ID")]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    [Display(Name = "Desde")]
    public DateTime From { get; set; } = DateTime.Today.AddDays(-7);

    [BindProperty(SupportsGet = true)]
    [Display(Name = "Hasta")]
    public DateTime To { get; set; } = DateTime.Today;

    public ApiPreviewEmployeeResponse? Preview { get; private set; }
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

        if (!ModelState.IsValid || From > To)
        {
            ErrorMessage = "Parametros invalidos.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(ApiBasicToken))
        {
            return RedirectToLogin();
        }

        Preview = await _apiClient.GetPreviewByEmployeeAsync(
            ApiBasicToken,
            UserId,
            From.Date,
            To.Date.AddDays(1).AddTicks(-1),
            cancellationToken);

        if (Preview is null)
        {
            ErrorMessage = "No se encontro informacion o la API rechazo la consulta.";
        }

        return Page();
    }
}