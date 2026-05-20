using System.ComponentModel.DataAnnotations;
using Checador.Web.Models;
using Checador.Web.Pages;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages.Ops;

[Authorize(Policy = "RestrictedModulesOnly")]
public sealed class IndexModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public IndexModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty(SupportsGet = true)]
    [Display(Name = "Filas historial")]
    public int Top { get; set; } = 10;

    [BindProperty]
    [Display(Name = "Intervalo AutoSync (min)")]
    [Range(1, 240, ErrorMessage = "El intervalo debe estar entre 1 y 240 minutos.")]
    public int IntervalMinutes { get; set; } = 15;

    public ApiOpsSyncStatusResponse? SyncStatus { get; private set; }
    public ApiOpsLiveStatusResponse? LiveStatus { get; private set; }
    public ApiOpsAutoSyncStatusResponse? AutoSyncStatus { get; private set; }
    public IReadOnlyList<ApiOpsHistoryItem> RecentHistory { get; private set; } = Array.Empty<ApiOpsHistoryItem>();

    public string? ErrorMessage { get; private set; }
    public string? OperationMessage { get; private set; }
    public bool OperationSuccess { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseOps();
        if (guard is not null)
        {
            return guard;
        }

        await LoadDashboardAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseOps();
        if (guard is not null)
        {
            return guard;
        }

        await LoadDashboardAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRunSyncAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseOps();
        if (guard is not null)
        {
            return guard;
        }

        var result = await _apiClient.RunOpsSyncAsync(ApiBasicToken!, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? (result.Data?.Message ?? "Sincronización ejecutada correctamente.")
            : $"No se pudo ejecutar sync ({result.StatusCode}): {result.Message}";

        await LoadDashboardAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSyncDeviceTimeAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseOps();
        if (guard is not null)
        {
            return guard;
        }

        var result = await _apiClient.SyncOpsDeviceTimeAsync(ApiBasicToken!, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? (result.Data?.Message ?? "Sincronización de hora ejecutada correctamente.")
            : $"No se pudo sincronizar hora ({result.StatusCode}): {result.Message}";

        await LoadDashboardAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAutoSyncStartAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseOps();
        if (guard is not null)
        {
            return guard;
        }

        var result = await _apiClient.StartOpsAutoSyncAsync(ApiBasicToken!, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? (result.Data?.LastMessage ?? "AutoSync activado correctamente.")
            : $"No se pudo activar AutoSync ({result.StatusCode}): {result.Message}";

        await LoadDashboardAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAutoSyncStopAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseOps();
        if (guard is not null)
        {
            return guard;
        }

        var result = await _apiClient.StopOpsAutoSyncAsync(ApiBasicToken!, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? (result.Data?.LastMessage ?? "AutoSync detenido correctamente.")
            : $"No se pudo detener AutoSync ({result.StatusCode}): {result.Message}";

        await LoadDashboardAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAutoSyncIntervalAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseOps();
        if (guard is not null)
        {
            return guard;
        }

        if (!ModelState.IsValid)
        {
            await LoadDashboardAsync(cancellationToken);
            OperationSuccess = false;
            OperationMessage = "Verifica el intervalo de AutoSync antes de guardar.";
            return Page();
        }

        var result = await _apiClient.SetOpsAutoSyncIntervalAsync(ApiBasicToken!, IntervalMinutes, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? (result.Data?.LastMessage ?? "Intervalo AutoSync actualizado.")
            : $"No se pudo actualizar intervalo AutoSync ({result.StatusCode}): {result.Message}";

        await LoadDashboardAsync(cancellationToken);
        return Page();
    }

    private IActionResult? EnsureCanUseOps()
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

    private async Task LoadDashboardAsync(CancellationToken cancellationToken)
    {
        Top = Top <= 0 ? 10 : Math.Min(Top, 200);

        SyncStatus = await _apiClient.GetOpsSyncStatusAsync(ApiBasicToken!, cancellationToken);
        LiveStatus = await _apiClient.GetOpsLiveStatusAsync(ApiBasicToken!, cancellationToken);
        AutoSyncStatus = await _apiClient.GetOpsAutoSyncStatusAsync(ApiBasicToken!, cancellationToken);
        RecentHistory = await _apiClient.GetOpsRecentHistoryAsync(ApiBasicToken!, Top, cancellationToken);

        if (AutoSyncStatus is not null)
        {
            IntervalMinutes = AutoSyncStatus.IntervalMinutes;
        }

        if (SyncStatus is null || LiveStatus is null || AutoSyncStatus is null)
        {
            ErrorMessage = "No se pudo cargar uno o más paneles de operación desde la API.";
        }
        else
        {
            ErrorMessage = null;
        }
    }
}
