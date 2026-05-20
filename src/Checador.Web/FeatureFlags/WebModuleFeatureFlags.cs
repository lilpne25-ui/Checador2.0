namespace Checador.Web.FeatureFlags;

public sealed class WebModuleFeatureFlags
{
    public bool EnableOps { get; set; } = true;
    public bool EnableEmployees { get; set; } = true;
    public bool EnableReassign { get; set; } = true;
    public bool EnablePervasive { get; set; } = true;
    public bool EnableManualPunch { get; set; } = true;
    public bool EnableSchedules { get; set; } = true;
    public bool EnableReports { get; set; } = true;
    public bool EnableAdminUsers { get; set; } = true;
}
