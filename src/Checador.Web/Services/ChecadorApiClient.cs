using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Checador.Web.Models;

namespace Checador.Web.Services;

public sealed class ChecadorApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ChecadorApiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<ApiLoginResponse?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithoutAuth();
        var response = await client.PostAsJsonAsync("/auth/login", new { username, password }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ApiLoginResponse>(cancellationToken: cancellationToken);
    }

    public string BuildApiUnavailableMessage()
        => $"No se pudo conectar con Checador.Api en {GetBaseUrl()}. Verifica que la API este corriendo antes de iniciar sesion.";

    public async Task<ApiKpiResponse?> GetKpiAsync(string basicToken, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var url = $"/kpi?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ApiKpiResponse>(cancellationToken: cancellationToken);
    }

    public async Task<ApiPreviewEmployeeResponse?> GetPreviewByEmployeeAsync(string basicToken, int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var url = $"/preview?userId={userId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ApiPreviewEmployeeResponse>(cancellationToken: cancellationToken);
    }

    public async Task<ApiClientCallResult<IReadOnlyList<ApiPreviewEmployeeResponse>>> GetPreviewAllAsync(string basicToken, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var url = $"/preview/all?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        var response = await client.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<List<ApiPreviewEmployeeResponse>>(cancellationToken: cancellationToken);
            return new ApiClientCallResult<IReadOnlyList<ApiPreviewEmployeeResponse>>
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                Message = "OK",
                Data = data is null ? Array.Empty<ApiPreviewEmployeeResponse>() : data
            };
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new ApiClientCallResult<IReadOnlyList<ApiPreviewEmployeeResponse>>
        {
            Success = false,
            StatusCode = (int)response.StatusCode,
            Message = ExtractMessage(body, response.ReasonPhrase),
            Data = Array.Empty<ApiPreviewEmployeeResponse>()
        };
    }

    public async Task<IReadOnlyList<ApiAdminUserResponse>> GetAdminUsersAsync(string basicToken, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.GetAsync("/admin/users", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<ApiAdminUserResponse>();
        }

        var data = await response.Content.ReadFromJsonAsync<List<ApiAdminUserResponse>>(cancellationToken: cancellationToken);
        return data is null ? Array.Empty<ApiAdminUserResponse>() : data;
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(string basicToken, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsJsonAsync(
            "/auth/change-password",
            new { currentPassword, newPassword },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return (true, "Password actualizada correctamente.");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, string.IsNullOrWhiteSpace(content) ? $"Error {response.StatusCode}" : content);
    }

    public async Task<ApiOpsSyncStatusResponse?> GetOpsSyncStatusAsync(string basicToken, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.GetAsync("/ops/sync/status", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ApiOpsSyncStatusResponse>(cancellationToken: cancellationToken);
    }

    public async Task<ApiOpsLiveStatusResponse?> GetOpsLiveStatusAsync(string basicToken, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.GetAsync("/ops/live-status", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ApiOpsLiveStatusResponse>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ApiOpsHistoryItem>> GetOpsRecentHistoryAsync(string basicToken, int top = 10, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var safeTop = top <= 0 ? 10 : Math.Min(top, 200);
        var response = await client.GetAsync($"/ops/recent-history?top={safeTop}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<ApiOpsHistoryItem>();
        }

        var data = await response.Content.ReadFromJsonAsync<List<ApiOpsHistoryItem>>(cancellationToken: cancellationToken);
        return data is null ? Array.Empty<ApiOpsHistoryItem>() : data;
    }

    public async Task<ApiClientCallResult<ApiOpsSyncRunResponse>> RunOpsSyncAsync(string basicToken, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsync("/ops/sync/run", content: null, cancellationToken);
        return await ReadCallResultAsync<ApiOpsSyncRunResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiOpsDeviceTimeSyncResponse>> SyncOpsDeviceTimeAsync(string basicToken, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsync("/ops/device/time/sync", content: null, cancellationToken);
        return await ReadCallResultAsync<ApiOpsDeviceTimeSyncResponse>(response, cancellationToken);
    }

    public async Task<ApiOpsAutoSyncStatusResponse?> GetOpsAutoSyncStatusAsync(string basicToken, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.GetAsync("/ops/sync/autosync/status", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ApiOpsAutoSyncStatusResponse>(cancellationToken: cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiOpsAutoSyncStatusResponse>> StartOpsAutoSyncAsync(string basicToken, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsync("/ops/sync/autosync/start", content: null, cancellationToken);
        return await ReadCallResultAsync<ApiOpsAutoSyncStatusResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiOpsAutoSyncStatusResponse>> StopOpsAutoSyncAsync(string basicToken, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsync("/ops/sync/autosync/stop", content: null, cancellationToken);
        return await ReadCallResultAsync<ApiOpsAutoSyncStatusResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiOpsAutoSyncStatusResponse>> SetOpsAutoSyncIntervalAsync(string basicToken, int intervalMinutes, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PutAsJsonAsync("/ops/sync/autosync/interval", new { intervalMinutes }, cancellationToken);
        return await ReadCallResultAsync<ApiOpsAutoSyncStatusResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiAdminEmployeeItemResponse>> GetAdminEmployeesAsync(string basicToken, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.GetAsync("/admin/employees", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<ApiAdminEmployeeItemResponse>();
        }

        var data = await response.Content.ReadFromJsonAsync<List<ApiAdminEmployeeItemResponse>>(cancellationToken: cancellationToken);
        return data is null ? Array.Empty<ApiAdminEmployeeItemResponse>() : data;
    }

    public async Task<ApiAdminEmployeeItemResponse?> GetAdminEmployeeAsync(string basicToken, int userId, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.GetAsync($"/admin/employees/{userId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ApiAdminEmployeeItemResponse>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ApiWorkScheduleResponse>> GetWorkSchedulesAsync(string basicToken, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.GetAsync("/admin/schedules", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<ApiWorkScheduleResponse>();
        }

        var data = await response.Content.ReadFromJsonAsync<List<ApiWorkScheduleResponse>>(cancellationToken: cancellationToken);
        return data is null ? Array.Empty<ApiWorkScheduleResponse>() : data;
    }

    public async Task<ApiClientCallResult<ApiWorkScheduleUpsertResponse>> UpsertWorkScheduleAsync(string basicToken, int code, ApiWorkScheduleUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PutAsJsonAsync($"/admin/schedules/{code}", request, cancellationToken);
        return await ReadCallResultAsync<ApiWorkScheduleUpsertResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiAdminEmployeeUpdateResponse>> UpdateAdminEmployeeAsync(string basicToken, int userId, ApiAdminEmployeeUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PutAsJsonAsync($"/admin/employees/{userId}", request, cancellationToken);
        return await ReadCallResultAsync<ApiAdminEmployeeUpdateResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiAdminEmployeeDeleteResponse>> DeleteAdminEmployeeAsync(string basicToken, int userId, bool deleteHistoryRows, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.DeleteAsync($"/admin/employees/{userId}?deleteHistoryRows={deleteHistoryRows.ToString().ToLowerInvariant()}", cancellationToken);
        return await ReadCallResultAsync<ApiAdminEmployeeDeleteResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiAdminManualPunchResponse>> CreateAdminManualPunchAsync(string basicToken, ApiAdminManualPunchRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsJsonAsync("/admin/manual-punch", request, cancellationToken);
        return await ReadCallResultAsync<ApiAdminManualPunchResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiAdminManualPunchResponse>> CreateAdminManualPunchBatchAsync(string basicToken, ApiAdminManualPunchBatchRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsJsonAsync("/admin/manual-punch/batch", request, cancellationToken);
        return await ReadCallResultAsync<ApiAdminManualPunchResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiAdminEmployeeReassignPreviewResponse>> PreviewAdminEmployeeReassignAsync(string basicToken, ApiAdminEmployeeReassignPreviewRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsJsonAsync("/admin/employees/reassign/preview", request, cancellationToken);
        return await ReadCallResultAsync<ApiAdminEmployeeReassignPreviewResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiAdminEmployeeReassignApplyResponse>> ApplyAdminEmployeeReassignAsync(string basicToken, ApiAdminEmployeeReassignApplyRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsJsonAsync("/admin/employees/reassign/apply", request, cancellationToken);
        return await ReadCallResultAsync<ApiAdminEmployeeReassignApplyResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiAdminPervasivePreviewResponse>> GetAdminPervasivePreviewAsync(string basicToken, int top = 500, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var safeTop = top <= 0 ? 500 : Math.Min(top, 5000);
        var response = await client.GetAsync($"/admin/pervasive/preview?top={safeTop}", cancellationToken);
        return await ReadCallResultAsync<ApiAdminPervasivePreviewResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiAdminPervasiveCleanupPreviewResponse>> PreviewAdminPervasiveNightCleanupAsync(string basicToken, ApiAdminPervasiveCleanupPreviewRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsJsonAsync("/admin/pervasive/night-cleanup/preview", request, cancellationToken);
        return await ReadCallResultAsync<ApiAdminPervasiveCleanupPreviewResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiAdminPervasiveCleanupRunResponse>> RunAdminPervasiveNightCleanupAsync(string basicToken, ApiAdminPervasiveCleanupRunRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsJsonAsync("/admin/pervasive/night-cleanup/run", request, cancellationToken);
        return await ReadCallResultAsync<ApiAdminPervasiveCleanupRunResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiReportGenerateResponse>> GenerateReportAsync(string basicToken, ApiReportGenerateRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsJsonAsync("/reports/generate", request, cancellationToken);
        return await ReadCallResultAsync<ApiReportGenerateResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiOvertimeFormatGenerateResponse>> GenerateOvertimeFormatAsync(string basicToken, ApiOvertimeFormatGenerateRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsJsonAsync("/reports/overtime-format/generate", request, cancellationToken);
        return await ReadCallResultAsync<ApiOvertimeFormatGenerateResponse>(response, cancellationToken);
    }

    public async Task<ApiClientCallResult<ApiLatenessReportGenerateResponse>> GenerateLatenessReportAsync(string basicToken, ApiLatenessReportGenerateRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsJsonAsync("/reports/lateness/generate", request, cancellationToken);
        return await ReadCallResultAsync<ApiLatenessReportGenerateResponse>(response, cancellationToken);
    }

    public async Task<ApiFileDownloadResult> DownloadGeneratedReportsAsync(string basicToken, IReadOnlyList<string> paths, string? exportFormat, CancellationToken cancellationToken = default)
    {
        var client = CreateClientWithAuth(basicToken);
        var response = await client.PostAsJsonAsync(
            "/reports/files/download",
            new { paths, exportFormat },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new ApiFileDownloadResult
            {
                Success = false,
                StatusCode = (int)response.StatusCode,
                Message = ExtractMessage(body, response.ReasonPhrase)
            };
        }

        return new ApiFileDownloadResult
        {
            Success = true,
            StatusCode = (int)response.StatusCode,
            Message = "OK",
            Content = await response.Content.ReadAsByteArrayAsync(cancellationToken),
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
            FileName = ResolveDownloadFileName(response, "reporte")
        };
    }

    public static string BuildBasicToken(string username, string password)
    {
        var raw = $"{username}:{password}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    private HttpClient CreateClientWithoutAuth()
    {
        var client = _httpClientFactory.CreateClient("ChecadorApi");
        client.BaseAddress = new Uri(GetBaseUrl());
        return client;
    }

    private HttpClient CreateClientWithAuth(string basicToken)
    {
        var client = CreateClientWithoutAuth();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
        return client;
    }

    private string GetBaseUrl()
    {
        var value = _configuration["WebApi:BaseUrl"];
        return string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:5085" : value.Trim().TrimEnd('/');
    }

    private static async Task<ApiClientCallResult<T>> ReadCallResultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return new ApiClientCallResult<T>
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                Message = "OK",
                Data = data
            };
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new ApiClientCallResult<T>
        {
            Success = false,
            StatusCode = (int)response.StatusCode,
            Message = ExtractMessage(body, response.ReasonPhrase),
            Data = default
        };
    }

    private static string ResolveDownloadFileName(HttpResponseMessage response, string fallback)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = disposition?.FileNameStar ?? disposition?.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return fallback;
        }

        return fileName.Trim().Trim('"');
    }

    private static string ExtractMessage(string? responseBody, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "Error sin detalle." : fallback;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("message", out var messageElement))
                {
                    var value = messageElement.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                if (doc.RootElement.TryGetProperty("Message", out var messageElementPascal))
                {
                    var value = messageElementPascal.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }
        catch
        {
            // ignore parse errors and fallback to raw body.
        }

        return responseBody.Trim();
    }
}

public sealed class ApiFileDownloadResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "reporte";
}
