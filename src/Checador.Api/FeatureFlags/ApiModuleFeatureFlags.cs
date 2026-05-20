namespace Checador.Api.FeatureFlags;

public sealed class ApiModuleFeatureFlags
{
    public bool EnablePreview { get; set; } = true;
    public bool EnableKpi { get; set; } = true;
    public bool EnableOps { get; set; } = true;
    public bool EnableOpsTelemetry { get; set; } = true;
    public bool EnableAdminUsers { get; set; } = true;
    public bool EnableAdminEmployees { get; set; } = true;
    public bool EnableAdminPervasive { get; set; } = true;
    public bool EnableAdminManualPunch { get; set; } = true;
    public bool EnableAdminSchedules { get; set; } = true;
    public bool EnableReports { get; set; } = true;
}
