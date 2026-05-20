using System.ComponentModel.DataAnnotations;
using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages.Admin;

[Authorize(Policy = "RestrictedModulesOnly")]
public sealed class PervasiveModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public PervasiveModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public PervasivePreviewInput PreviewInput { get; set; } = new();

    [BindProperty]
    public PervasiveCleanupInput CleanupInput { get; set; } = new();

    [BindProperty]
    [Display(Name = "Confirmo que revisé el preview y quiero ejecutar la limpieza")]
    public bool ConfirmApply { get; set; }

    [BindProperty]
    [Display(Name = "Escribe exactamente 'EJECUTAR LIMPIEZA'")]
    public string ConfirmText { get; set; } = string.Empty;

    public ApiAdminPervasivePreviewResponse? PreviewResult { get; private set; }
    public ApiAdminPervasiveCleanupPreviewResponse? CleanupPreviewResult { get; private set; }
    public ApiAdminPervasiveCleanupRunResponse? CleanupRunResult { get; private set; }

    public string? ErrorMessage { get; private set; }
    public string? OperationMessage { get; private set; }
    public bool OperationSuccess { get; private set; }

    public IActionResult OnGet()
    {
        var guard = EnsureCanUseAdmin();
        if (guard is not null)
        {
            return guard;
        }

        SetDefaultInputs();
        return Page();
    }

    public async Task<IActionResult> OnPostLoadPreviewAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseAdmin();
        if (guard is not null)
        {
            return guard;
        }

        SetDefaultInputs();

        if (PreviewInput.Top is < 1 or > 5000)
        {
            ErrorMessage = "Top debe estar entre 1 y 5000 filas.";
            return Page();
        }

        var result = await _apiClient.GetAdminPervasivePreviewAsync(ApiBasicToken!, PreviewInput.Top, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? $"Preview Pervasive cargado ({result.Data?.ReturnedRows ?? 0} fila(s))."
            : $"No se pudo cargar preview ({result.StatusCode}): {result.Message}";

        PreviewResult = result.Data;
        return Page();
    }

    public async Task<IActionResult> OnPostPreviewCleanupAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseAdmin();
        if (guard is not null)
        {
            return guard;
        }

        SetDefaultInputs();

        if (CleanupInput.UserId.HasValue && CleanupInput.UserId.Value <= 0)
        {
            ErrorMessage = "UserId debe ser mayor que 0 cuando se especifica.";
            return Page();
        }

        if (CleanupInput.FromDate.Date > CleanupInput.ToDate.Date)
        {
            ErrorMessage = "Rango inválido: fecha inicial no puede ser mayor a fecha final.";
            return Page();
        }

        var request = new ApiAdminPervasiveCleanupPreviewRequest
        {
            From = CleanupInput.FromDate.Date,
            To = CleanupInput.ToDate.Date,
            UserId = CleanupInput.UserId
        };

        var result = await _apiClient.PreviewAdminPervasiveNightCleanupAsync(ApiBasicToken!, request, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? "Preview de limpieza nocturna generado correctamente."
            : $"No se pudo generar preview ({result.StatusCode}): {result.Message}";

        CleanupPreviewResult = result.Data;
        ConfirmApply = false;
        ConfirmText = string.Empty;

        return Page();
    }

    public async Task<IActionResult> OnPostRunCleanupAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseAdmin();
        if (guard is not null)
        {
            return guard;
        }

        SetDefaultInputs();

        if (CleanupInput.UserId.HasValue && CleanupInput.UserId.Value <= 0)
        {
            ErrorMessage = "UserId debe ser mayor que 0 cuando se especifica.";
            return Page();
        }

        if (CleanupInput.FromDate.Date > CleanupInput.ToDate.Date)
        {
            ErrorMessage = "Rango inválido: fecha inicial no puede ser mayor a fecha final.";
            return Page();
        }

        if (!ConfirmApply || !string.Equals(ConfirmText?.Trim(), "EJECUTAR LIMPIEZA", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Debes confirmar la acción y escribir exactamente 'EJECUTAR LIMPIEZA'.";
            return Page();
        }

        var request = new ApiAdminPervasiveCleanupRunRequest
        {
            From = CleanupInput.FromDate.Date,
            To = CleanupInput.ToDate.Date,
            UserId = CleanupInput.UserId,
            ConfirmApply = true,
            ConfirmText = ConfirmText.Trim()
        };

        var result = await _apiClient.RunAdminPervasiveNightCleanupAsync(ApiBasicToken!, request, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? "Limpieza nocturna ejecutada correctamente."
            : $"No se pudo ejecutar limpieza ({result.StatusCode}): {result.Message}";

        CleanupRunResult = result.Data;

        var previewRefresh = await _apiClient.PreviewAdminPervasiveNightCleanupAsync(
            ApiBasicToken!,
            new ApiAdminPervasiveCleanupPreviewRequest
            {
                From = CleanupInput.FromDate.Date,
                To = CleanupInput.ToDate.Date,
                UserId = CleanupInput.UserId
            },
            cancellationToken);

        CleanupPreviewResult = previewRefresh.Data;
        ConfirmApply = false;
        ConfirmText = string.Empty;

        return Page();
    }

    private IActionResult? EnsureCanUseAdmin()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToLogin();
        }

        if (MustChangePassword)
        {
            return RedirectToPasswordChange();
        }

        if (string.IsNullOrWhiteSpace(ApiBasicToken))
        {
            return RedirectToLogin();
        }

        return null;
    }

    private void SetDefaultInputs()
    {
        PreviewInput.Top = PreviewInput.Top <= 0 ? 200 : Math.Min(PreviewInput.Top, 5000);

        if (CleanupInput.FromDate == default || CleanupInput.ToDate == default)
        {
            var today = DateTime.Today;
            CleanupInput.FromDate = today.AddDays(-30);
            CleanupInput.ToDate = today;
        }
    }

    public sealed class PervasivePreviewInput
    {
        [Display(Name = "Filas a mostrar")]
        public int Top { get; set; } = 200;
    }

    public sealed class PervasiveCleanupInput
    {
        [DataType(DataType.Date)]
        [Display(Name = "Desde")]
        public DateTime FromDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Hasta")]
        public DateTime ToDate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "UserId debe ser mayor que 0.")]
        [Display(Name = "USERID (opcional)")]
        public int? UserId { get; set; }
    }
}
