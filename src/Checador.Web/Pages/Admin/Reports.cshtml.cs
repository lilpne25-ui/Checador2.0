using System.ComponentModel.DataAnnotations;
using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public sealed class ReportsModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public ReportsModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public ReportsInput Input { get; set; } = new();

    [BindProperty]
    public OvertimeFormatInput OvertimeInput { get; set; } = new();

    [BindProperty]
    public LatenessReportInput LatenessInput { get; set; } = new();

    [BindProperty]
    public string? SubmittedForm { get; set; }

    public IReadOnlyList<ApiAdminEmployeeItemResponse> Employees { get; private set; } = Array.Empty<ApiAdminEmployeeItemResponse>();
    public string? ErrorMessage { get; private set; }
    public string? OperationMessage { get; private set; }
    public bool OperationSuccess { get; private set; }
    public ApiReportGenerateResponse? LastResult { get; private set; }
    public ApiOvertimeFormatGenerateResponse? LastOvertimeResult { get; private set; }
    public ApiLatenessReportGenerateResponse? LastLatenessResult { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var guard = await EnsureCanUseAdminAsync(cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        EnsureInputDefaults();
        return Page();
    }

    public async Task<IActionResult> OnPostGenerateReportAsync(CancellationToken cancellationToken)
    {
        var guard = await EnsureCanUseAdminAsync(cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        if (IsSubmittedForm("overtime"))
        {
            return await OnPostGenerateOvertimeAsync(cancellationToken);
        }

        if (IsSubmittedForm("lateness"))
        {
            return await OnPostGenerateLatenessAsync(cancellationToken);
        }

        RemoveValidationForPrefixes(nameof(OvertimeInput), nameof(LatenessInput));
        EnsureInputDefaults();

        if (!ValidateGeneralInput(out var generalValidationError))
        {
            ErrorMessage = generalValidationError;
            return Page();
        }

        if (Input.FromDate.Date > Input.ToDate.Date)
        {
            ErrorMessage = "Rango invalido: fecha inicial no puede ser mayor a fecha final.";
            return Page();
        }

        var toInclusive = Input.ToDate.Date.AddDays(1).AddTicks(-1);
        var request = new ApiReportGenerateRequest
        {
            UserId = Input.UserId > 0 ? Input.UserId : null,
            From = Input.FromDate.Date,
            To = toInclusive,
            OutputFormat = ResolveGeneralApiFormat(Input.OutputFormat),
            OutputDirectory = string.IsNullOrWhiteSpace(Input.OutputDirectory) ? null : Input.OutputDirectory.Trim(),
            Company = string.IsNullOrWhiteSpace(Input.Company) ? null : Input.Company.Trim(),
            UseSimulatedDataWhenEmpty = Input.UseSimulatedDataWhenEmpty
        };

        var result = await _apiClient.GenerateReportAsync(ApiBasicToken!, request, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? "Reporte general generado correctamente."
            : $"No se pudo generar ({result.StatusCode}): {result.Message}";
        LastResult = result.Data;

        if (result.Success && result.Data?.ReportPaths.Count > 0)
        {
            var download = await TryDownloadReportsAsync(
                result.Data.ReportPaths,
                ResolveDownloadFormat(Input.OutputFormat),
                "reporte general",
                cancellationToken);
            if (download is not null)
            {
                return download;
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateOvertimeAsync(CancellationToken cancellationToken)
    {
        var guard = await EnsureCanUseAdminAsync(cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        if (IsSubmittedForm("general"))
        {
            return await OnPostGenerateReportAsync(cancellationToken);
        }

        if (IsSubmittedForm("lateness"))
        {
            return await OnPostGenerateLatenessAsync(cancellationToken);
        }

        RemoveValidationForPrefixes(nameof(Input), nameof(LatenessInput));
        EnsureInputDefaults();

        if (!ValidateOvertimeInput(out var overtimeValidationError))
        {
            ErrorMessage = overtimeValidationError;
            return Page();
        }

        if (OvertimeInput.FromDate.Date > OvertimeInput.ToDate.Date)
        {
            ErrorMessage = "Rango invalido en horas extras: fecha inicial no puede ser mayor a fecha final.";
            return Page();
        }

        var selectedEmployee = Employees.FirstOrDefault(x => x.UserId == OvertimeInput.UserId);
        var request = new ApiOvertimeFormatGenerateRequest
        {
            UserId = OvertimeInput.UserId,
            EmployeeName = selectedEmployee?.EmployeeName,
            WorkOrderNumber = OvertimeInput.WorkOrderNumber.Trim(),
            WorkDescription = OvertimeInput.WorkDescription.Trim(),
            RequestDate = OvertimeInput.RequestDate.Date,
            From = OvertimeInput.FromDate.Date,
            To = OvertimeInput.ToDate.Date,
            ImmediateBossName = OvertimeInput.ImmediateBossName.Trim(),
            Notes = string.IsNullOrWhiteSpace(OvertimeInput.Notes) ? null : OvertimeInput.Notes.Trim(),
            OutputDirectory = string.IsNullOrWhiteSpace(OvertimeInput.OutputDirectory) ? null : OvertimeInput.OutputDirectory.Trim(),
            FileName = string.IsNullOrWhiteSpace(OvertimeInput.FileName) ? null : OvertimeInput.FileName.Trim(),
            UseSimulatedDataWhenEmpty = OvertimeInput.UseSimulatedDataWhenEmpty
        };

        var result = await _apiClient.GenerateOvertimeFormatAsync(ApiBasicToken!, request, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? "Formato de horas extras generado correctamente."
            : $"No se pudo generar horas extras ({result.StatusCode}): {result.Message}";
        LastOvertimeResult = result.Data;

        if (result.Success && !string.IsNullOrWhiteSpace(result.Data?.OutputPath))
        {
            var download = await TryDownloadReportsAsync(
                new[] { result.Data.OutputPath },
                ResolveDownloadFormat(OvertimeInput.OutputFormat),
                "formato de horas extras",
                cancellationToken);
            if (download is not null)
            {
                return download;
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateLatenessAsync(CancellationToken cancellationToken)
    {
        var guard = await EnsureCanUseAdminAsync(cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        if (IsSubmittedForm("general"))
        {
            return await OnPostGenerateReportAsync(cancellationToken);
        }

        if (IsSubmittedForm("overtime"))
        {
            return await OnPostGenerateOvertimeAsync(cancellationToken);
        }

        RemoveValidationForPrefixes(nameof(Input), nameof(OvertimeInput));
        EnsureInputDefaults();

        if (!ValidateLatenessInput(out var latenessValidationError))
        {
            ErrorMessage = latenessValidationError;
            return Page();
        }

        if (LatenessInput.FromDate.Date > LatenessInput.ToDate.Date)
        {
            ErrorMessage = "Rango invalido en retardos: fecha inicial no puede ser mayor a fecha final.";
            return Page();
        }

        var request = new ApiLatenessReportGenerateRequest
        {
            UserId = LatenessInput.UserId > 0 ? LatenessInput.UserId : null,
            From = LatenessInput.FromDate.Date,
            To = LatenessInput.ToDate.Date,
            OutputDirectory = string.IsNullOrWhiteSpace(LatenessInput.OutputDirectory) ? null : LatenessInput.OutputDirectory.Trim(),
            FileName = string.IsNullOrWhiteSpace(LatenessInput.FileName) ? null : LatenessInput.FileName.Trim(),
            UseSimulatedDataWhenEmpty = LatenessInput.UseSimulatedDataWhenEmpty
        };

        var result = await _apiClient.GenerateLatenessReportAsync(ApiBasicToken!, request, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? "Reporte de retardos generado correctamente."
            : $"No se pudo generar retardos ({result.StatusCode}): {result.Message}";
        LastLatenessResult = result.Data;

        if (result.Success && !string.IsNullOrWhiteSpace(result.Data?.OutputPath))
        {
            var download = await TryDownloadReportsAsync(
                new[] { result.Data.OutputPath },
                ResolveDownloadFormat(LatenessInput.OutputFormat),
                "reporte de retardos",
                cancellationToken);
            if (download is not null)
            {
                return download;
            }
        }

        return Page();
    }

    private async Task<IActionResult?> EnsureCanUseAdminAsync(CancellationToken cancellationToken)
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

        Employees = await _apiClient.GetAdminEmployeesAsync(ApiBasicToken, cancellationToken);
        EnsureInputDefaults();
        return null;
    }

    private void RemoveValidationForPrefixes(params string[] prefixes)
    {
        if (prefixes is null || prefixes.Length == 0)
        {
            return;
        }

        var keysToRemove = ModelState.Keys
            .Where(key => prefixes.Any(prefix =>
                string.Equals(key, prefix, StringComparison.Ordinal)
                || key.StartsWith(prefix + ".", StringComparison.Ordinal)))
            .ToList();

        foreach (var key in keysToRemove)
        {
            ModelState.Remove(key);
        }
    }

    private bool IsSubmittedForm(string formName)
    {
        return string.Equals(SubmittedForm, formName, StringComparison.OrdinalIgnoreCase);
    }

    private bool ValidateGeneralInput(out string errorMessage)
    {
        if (Input.FromDate == default || Input.ToDate == default)
        {
            errorMessage = "Corrige las fechas del reporte general antes de generar.";
            return false;
        }

        if (!IsSupportedDownloadFormat(Input.OutputFormat, allowExcelAlias: true))
        {
            errorMessage = "Formato invalido. Usa XLSX, CSV o PDF.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private bool ValidateOvertimeInput(out string errorMessage)
    {
        if (OvertimeInput.UserId <= 0)
        {
            errorMessage = "Selecciona un empleado valido para el formato de horas extras.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OvertimeInput.WorkOrderNumber))
        {
            errorMessage = "Captura la orden de trabajo (OT).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OvertimeInput.WorkDescription))
        {
            errorMessage = "Captura el trabajo a realizar.";
            return false;
        }

        if (OvertimeInput.RequestDate == default || OvertimeInput.FromDate == default || OvertimeInput.ToDate == default)
        {
            errorMessage = "Corrige las fechas del formato de horas extras.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OvertimeInput.ImmediateBossName))
        {
            errorMessage = "Captura el nombre del jefe inmediato.";
            return false;
        }

        if (!IsSupportedDownloadFormat(OvertimeInput.OutputFormat))
        {
            errorMessage = "Formato invalido en horas extras. Usa XLSX, CSV o PDF.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private bool ValidateLatenessInput(out string errorMessage)
    {
        if (LatenessInput.FromDate == default || LatenessInput.ToDate == default)
        {
            errorMessage = "Corrige las fechas del reporte de retardos antes de generar.";
            return false;
        }

        if (!IsSupportedDownloadFormat(LatenessInput.OutputFormat))
        {
            errorMessage = "Formato invalido en retardos. Usa XLSX, CSV o PDF.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private void EnsureInputDefaults()
    {
        var today = DateTime.Today;

        if (Input.FromDate == default)
        {
            Input.FromDate = new DateTime(today.Year, today.Month, 1);
        }

        if (Input.ToDate == default)
        {
            Input.ToDate = today;
        }

        if (string.IsNullOrWhiteSpace(Input.OutputFormat))
        {
            Input.OutputFormat = "xlsx";
        }

        if (string.IsNullOrWhiteSpace(OvertimeInput.OutputFormat))
        {
            OvertimeInput.OutputFormat = "xlsx";
        }

        if (string.IsNullOrWhiteSpace(LatenessInput.OutputFormat))
        {
            LatenessInput.OutputFormat = "xlsx";
        }

        if (OvertimeInput.RequestDate == default)
        {
            OvertimeInput.RequestDate = today;
        }

        if (OvertimeInput.FromDate == default)
        {
            OvertimeInput.FromDate = today;
        }

        if (OvertimeInput.ToDate == default)
        {
            OvertimeInput.ToDate = today;
        }

        if (string.IsNullOrWhiteSpace(OvertimeInput.ImmediateBossName))
        {
            OvertimeInput.ImmediateBossName = "Jefe inmediato";
        }

        if (LatenessInput.FromDate == default)
        {
            LatenessInput.FromDate = new DateTime(today.Year, today.Month, 1);
        }

        if (LatenessInput.ToDate == default)
        {
            LatenessInput.ToDate = today;
        }
    }

    private async Task<IActionResult?> TryDownloadReportsAsync(
        IReadOnlyList<string> paths,
        string exportFormat,
        string reportLabel,
        CancellationToken cancellationToken)
    {
        var download = await _apiClient.DownloadGeneratedReportsAsync(ApiBasicToken!, paths, exportFormat, cancellationToken);
        if (download.Success)
        {
            return File(download.Content, download.ContentType, download.FileName);
        }

        OperationSuccess = false;
        OperationMessage = $"Se genero el {reportLabel}, pero no se pudo descargar ({download.StatusCode}): {download.Message}";
        return null;
    }

    private static string ResolveGeneralApiFormat(string? outputFormat)
        => string.Equals(outputFormat, "pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "excel";

    private static string ResolveDownloadFormat(string? outputFormat)
    {
        var normalized = outputFormat?.Trim().ToLowerInvariant();
        return normalized is "csv" or "pdf" ? normalized : "source";
    }

    private static bool IsSupportedDownloadFormat(string? outputFormat, bool allowExcelAlias = false)
    {
        var normalized = outputFormat?.Trim().ToLowerInvariant();
        return normalized is "xlsx" or "csv" or "pdf" || (allowExcelAlias && normalized == "excel");
    }

    public sealed class ReportsInput
    {
        [Display(Name = "Empleado")]
        public int? UserId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Desde")]
        public DateTime FromDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Hasta")]
        public DateTime ToDate { get; set; }

        [Required]
        [RegularExpression("^(excel|xlsx|csv|pdf)$", ErrorMessage = "Formato debe ser XLSX, CSV o PDF.")]
        [Display(Name = "Formato")]
        public string OutputFormat { get; set; } = "xlsx";

        [StringLength(300)]
        [Display(Name = "Directorio temporal servidor (opcional)")]
        public string? OutputDirectory { get; set; }

        [StringLength(150)]
        [Display(Name = "Empresa (opcional)")]
        public string? Company { get; set; }

        [Display(Name = "Usar datos simulados si no hay registros")]
        public bool UseSimulatedDataWhenEmpty { get; set; } = true;
    }

    public sealed class OvertimeFormatInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "Selecciona un empleado valido.")]
        [Display(Name = "Empleado")]
        public int UserId { get; set; }

        [Required]
        [StringLength(60)]
        [Display(Name = "Orden de trabajo (OT)")]
        public string WorkOrderNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        [Display(Name = "Trabajo a realizar")]
        public string WorkDescription { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de solicitud")]
        public DateTime RequestDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Desde")]
        public DateTime FromDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Hasta")]
        public DateTime ToDate { get; set; }

        [Required]
        [StringLength(120)]
        [Display(Name = "Jefe inmediato")]
        public string ImmediateBossName { get; set; } = string.Empty;

        [StringLength(250)]
        [Display(Name = "Observaciones")]
        public string? Notes { get; set; }

        [RegularExpression("^(xlsx|csv|pdf)$", ErrorMessage = "Formato debe ser XLSX, CSV o PDF.")]
        [Display(Name = "Formato")]
        public string OutputFormat { get; set; } = "xlsx";

        [Display(Name = "Usar datos simulados si no hay registros")]
        public bool UseSimulatedDataWhenEmpty { get; set; } = true;

        [StringLength(300)]
        [Display(Name = "Directorio temporal servidor (opcional)")]
        public string? OutputDirectory { get; set; }

        [StringLength(120)]
        [Display(Name = "Nombre de archivo (opcional)")]
        public string? FileName { get; set; }
    }

    public sealed class LatenessReportInput
    {
        [Display(Name = "Empleado (opcional)")]
        public int? UserId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Desde")]
        public DateTime FromDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Hasta")]
        public DateTime ToDate { get; set; }

        [StringLength(300)]
        [Display(Name = "Directorio temporal servidor (opcional)")]
        public string? OutputDirectory { get; set; }

        [StringLength(120)]
        [Display(Name = "Nombre de archivo (opcional)")]
        public string? FileName { get; set; }

        [RegularExpression("^(xlsx|csv|pdf)$", ErrorMessage = "Formato debe ser XLSX, CSV o PDF.")]
        [Display(Name = "Formato")]
        public string OutputFormat { get; set; } = "xlsx";

        [Display(Name = "Usar datos simulados si no hay registros")]
        public bool UseSimulatedDataWhenEmpty { get; set; } = true;
    }
}
