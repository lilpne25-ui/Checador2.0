using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages;

public sealed class PreviewAllModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public PreviewAllModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty(SupportsGet = true)]
    [Display(Name = "Desde")]
    public DateTime From { get; set; } = DateTime.Today.AddDays(-7);

    [BindProperty(SupportsGet = true)]
    [Display(Name = "Hasta")]
    public DateTime To { get; set; } = DateTime.Today;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SearchBy { get; set; } = "TODOS";

    [BindProperty(SupportsGet = true)]
    public string Status { get; set; } = "TODOS";

    [BindProperty(SupportsGet = true)]
    public int? SelectedUserId { get; set; }

    public IReadOnlyList<ApiPreviewEmployeeResponse> Previews { get; private set; } = Array.Empty<ApiPreviewEmployeeResponse>();
    public IReadOnlyList<ApiPreviewEmployeeResponse> VisiblePreviews { get; private set; } = Array.Empty<ApiPreviewEmployeeResponse>();
    public ApiPreviewEmployeeResponse? SelectedPreview { get; private set; }
    public string? ErrorMessage { get; private set; }

    public int TotalEmployees => Previews.Count;
    public decimal TotalWorkedHours => Previews.Sum(x => x.TotalWorkedHours);
    public decimal TotalExtraHours => Previews.Sum(x => x.TotalExtraHours);
    public int TotalIncidences => Previews.Sum(GetIncidentCount);
    public int TotalNoRecordDays => Previews.Sum(GetNoRecordCount);

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

        ApiClientCallResult<IReadOnlyList<ApiPreviewEmployeeResponse>> previewResult;
        try
        {
            previewResult = await _apiClient.GetPreviewAllAsync(ApiBasicToken, From.Date, To.Date.AddDays(1).AddTicks(-1), cancellationToken);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = _apiClient.BuildApiUnavailableMessage();
            return Page();
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = _apiClient.BuildApiUnavailableMessage();
            return Page();
        }

        if (!previewResult.Success)
        {
            ErrorMessage = previewResult.StatusCode switch
            {
                StatusCodes.Status401Unauthorized => "Sesion API invalida (401). Cierra sesion y vuelve a iniciar.",
                StatusCodes.Status403Forbidden => "Acceso denegado por API (403). Valida permisos del usuario.",
                StatusCodes.Status503ServiceUnavailable => $"Modulo temporalmente deshabilitado en API (503): {previewResult.Message}",
                _ => $"Error API ({previewResult.StatusCode}): {previewResult.Message}"
            };
            Previews = Array.Empty<ApiPreviewEmployeeResponse>();
            BuildViewState();
            return Page();
        }

        Previews = previewResult.Data ?? Array.Empty<ApiPreviewEmployeeResponse>();
        if (Previews.Count == 0)
        {
            ErrorMessage = "Sin datos para el rango seleccionado.";
        }

        BuildViewState();
        return Page();
    }

    public string GetEmployeeStatusKey(ApiPreviewEmployeeResponse item)
    {
        if (item.Days.Count == 0 || GetNoRecordCount(item) >= item.Days.Count)
        {
            return "SIN_REGISTRO";
        }

        return GetIncidentCount(item) > 0 || GetNoRecordCount(item) > 0 ? "REVISAR" : "OK";
    }

    public string GetEmployeeStatusCss(ApiPreviewEmployeeResponse item)
    {
        return GetEmployeeStatusKey(item) switch
        {
            "OK" => "is-good",
            "SIN_REGISTRO" => "is-warning",
            _ => "is-info"
        };
    }

    public int GetWorkedDayCount(ApiPreviewEmployeeResponse item)
    {
        return item.Days.Count(day => !IsNoRecordDay(day) && day.HorasTrabajadas.GetValueOrDefault() > 0);
    }

    public int GetIncidentCount(ApiPreviewEmployeeResponse item)
    {
        return item.Days.Count(IsIncidentDay);
    }

    public int GetNoRecordCount(ApiPreviewEmployeeResponse item)
    {
        return item.Days.Count(IsNoRecordDay);
    }

    public string GetDayStatusLabel(ApiReportDay day)
    {
        if (IsNoRecordDay(day))
        {
            return "SIN_REGISTRO";
        }

        return string.IsNullOrWhiteSpace(day.Estatus) ? "OK" : day.Estatus.Trim();
    }

    public string GetDayStatusCss(ApiReportDay day)
    {
        if (IsNoRecordDay(day))
        {
            return "is-warning";
        }

        return IsIncidentDay(day) ? "is-danger" : "is-good";
    }

    public string GetDayObservation(ApiReportDay day)
    {
        if (IsNoRecordDay(day))
        {
            return "Sin registro disponible para este dia.";
        }

        if (IsIncidentDay(day))
        {
            return $"Revisar estatus reportado por API: {GetDayStatusLabel(day)}.";
        }

        return "Jornada calculada por la API interna.";
    }

    private void BuildViewState()
    {
        SearchBy = NormalizeSearchBy(SearchBy);
        Status = NormalizeStatusFilter(Status);
        Search = Search?.Trim();

        IEnumerable<ApiPreviewEmployeeResponse> query = Previews;

        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(item => MatchesSearch(item, Search, SearchBy));
        }

        if (!string.Equals(Status, "TODOS", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item => string.Equals(GetEmployeeStatusKey(item), Status, StringComparison.OrdinalIgnoreCase));
        }

        VisiblePreviews = query.ToList();

        SelectedPreview = SelectedUserId.HasValue
            ? Previews.FirstOrDefault(x => x.UserId == SelectedUserId.Value)
            : VisiblePreviews.FirstOrDefault();

        SelectedUserId = SelectedPreview?.UserId;
    }

    private static bool MatchesSearch(ApiPreviewEmployeeResponse item, string search, string searchBy)
    {
        return searchBy switch
        {
            "USERID" => item.UserId.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase),
            "NOMBRE" => item.EmployeeName.Contains(search, StringComparison.OrdinalIgnoreCase),
            _ => item.UserId.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.EmployeeName.Contains(search, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string NormalizeSearchBy(string? value)
    {
        return string.Equals(value, "USERID", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "NOMBRE", StringComparison.OrdinalIgnoreCase)
            ? value!.ToUpperInvariant()
            : "TODOS";
    }

    private static string NormalizeStatusFilter(string? value)
    {
        return string.Equals(value, "OK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "REVISAR", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "SIN_REGISTRO", StringComparison.OrdinalIgnoreCase)
            ? value!.ToUpperInvariant()
            : "TODOS";
    }

    private static bool IsNoRecordDay(ApiReportDay day)
    {
        var status = day.Estatus?.Trim();
        return status?.Contains("SIN", StringComparison.OrdinalIgnoreCase) == true
            || (string.IsNullOrWhiteSpace(status)
                && day.HorasTrabajadas.GetValueOrDefault() == 0
                && day.HorasExtra.GetValueOrDefault() == 0);
    }

    private static bool IsIncidentDay(ApiReportDay day)
    {
        if (IsNoRecordDay(day))
        {
            return false;
        }

        var status = day.Estatus?.Trim();
        return !string.IsNullOrWhiteSpace(status)
            && !string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase);
    }
}
