using System.ComponentModel.DataAnnotations;
using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages.Admin;

[Authorize(Policy = "RestrictedModulesOnly")]
public sealed class ReassignModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public ReassignModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public ReassignInput Input { get; set; } = new();

    [BindProperty]
    [Display(Name = "Escribe exactamente 'APLICAR CAMBIO'")]
    public string ConfirmText { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "Confirmo que revisé el preview y quiero aplicar el cambio")]
    public bool ConfirmApply { get; set; }

    public string? ErrorMessage { get; private set; }
    public string? OperationMessage { get; private set; }
    public bool OperationSuccess { get; private set; }
    public ApiAdminEmployeeReassignPreviewResponse? PreviewResult { get; private set; }
    public ApiAdminEmployeeReassignApplyResponse? ApplyResult { get; private set; }

    public IActionResult OnGet()
    {
        var guard = EnsureCanUseAdmin();
        if (guard is not null)
        {
            return guard;
        }

        if (Input.FromDate == default)
        {
            var today = DateTime.Today;
            Input.FromDate = today.AddDays(-14);
            Input.ToDate = today;
            Input.AllowSkipConflicts = true;
            Input.IncludePervasive = false;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseAdmin();
        if (guard is not null)
        {
            return guard;
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Corrige los datos antes de ejecutar preview.";
            return Page();
        }

        if (Input.FromDate.Date > Input.ToDate.Date)
        {
            ErrorMessage = "Rango inválido: fecha inicial no puede ser mayor a fecha final.";
            return Page();
        }

        var request = new ApiAdminEmployeeReassignPreviewRequest
        {
            OldUserId = Input.OldUserId,
            NewUserId = Input.NewUserId,
            From = Input.FromDate.Date,
            To = Input.ToDate.Date,
            IncludePervasive = Input.IncludePervasive
        };

        var result = await _apiClient.PreviewAdminEmployeeReassignAsync(ApiBasicToken!, request, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? "Preview ejecutado correctamente."
            : $"No se pudo ejecutar preview ({result.StatusCode}): {result.Message}";

        PreviewResult = result.Data;
        return Page();
    }

    public async Task<IActionResult> OnPostApplyAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseAdmin();
        if (guard is not null)
        {
            return guard;
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Corrige los datos antes de aplicar.";
            return Page();
        }

        if (!ConfirmApply || !string.Equals(ConfirmText?.Trim(), "APLICAR CAMBIO", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Debes confirmar la acción y escribir exactamente 'APLICAR CAMBIO'.";
            return Page();
        }

        var request = new ApiAdminEmployeeReassignApplyRequest
        {
            OldUserId = Input.OldUserId,
            NewUserId = Input.NewUserId,
            From = Input.FromDate.Date,
            To = Input.ToDate.Date,
            NewName = string.IsNullOrWhiteSpace(Input.NewName) ? null : Input.NewName.Trim(),
            NewDepartment = string.IsNullOrWhiteSpace(Input.NewDepartment) ? null : Input.NewDepartment.Trim(),
            NewPervasiveDept = string.IsNullOrWhiteSpace(Input.NewPervasiveDept) ? null : Input.NewPervasiveDept.Trim(),
            NewPervasiveShift = string.IsNullOrWhiteSpace(Input.NewPervasiveShift) ? null : Input.NewPervasiveShift.Trim(),
            AllowSkipConflicts = Input.AllowSkipConflicts,
            IncludePervasive = Input.IncludePervasive,
            ConfirmApply = true,
            ConfirmText = ConfirmText.Trim()
        };

        var result = await _apiClient.ApplyAdminEmployeeReassignAsync(ApiBasicToken!, request, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? "Cambio por rango aplicado correctamente."
            : $"No se pudo aplicar cambio ({result.StatusCode}): {result.Message}";

        ApplyResult = result.Data;
        if (!result.Success)
        {
            return Page();
        }

        var previewResult = await _apiClient.PreviewAdminEmployeeReassignAsync(ApiBasicToken!, new ApiAdminEmployeeReassignPreviewRequest
        {
            OldUserId = Input.OldUserId,
            NewUserId = Input.NewUserId,
            From = Input.FromDate.Date,
            To = Input.ToDate.Date,
            IncludePervasive = Input.IncludePervasive
        }, cancellationToken);

        PreviewResult = previewResult.Data;
        ConfirmText = string.Empty;
        ConfirmApply = false;

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

    public sealed class ReassignInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "OldUserId debe ser mayor a 0.")]
        [Display(Name = "USERID origen")]
        public int OldUserId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "NewUserId debe ser mayor a 0.")]
        [Display(Name = "USERID destino")]
        public int NewUserId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Desde")]
        public DateTime FromDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Hasta")]
        public DateTime ToDate { get; set; }

        [StringLength(128)]
        [Display(Name = "Nuevo nombre (opcional)")]
        public string? NewName { get; set; }

        [StringLength(64)]
        [Display(Name = "Nuevo depto (opcional)")]
        public string? NewDepartment { get; set; }

        [StringLength(64)]
        [Display(Name = "Nuevo depto Pervasive (opcional)")]
        public string? NewPervasiveDept { get; set; }

        [StringLength(64)]
        [Display(Name = "Nuevo turno Pervasive (opcional)")]
        public string? NewPervasiveShift { get; set; }

        [Display(Name = "Omitir conflictos automáticamente")]
        public bool AllowSkipConflicts { get; set; } = true;

        [Display(Name = "Incluir Pervasive")]
        public bool IncludePervasive { get; set; }
    }
}
