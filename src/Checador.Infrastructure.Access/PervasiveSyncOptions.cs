namespace Checador.Infrastructure.Access;

public sealed class PervasiveSyncOptions
{
    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public string AttendanceTable { get; set; } = "TIME_ATTENDANCE";
    public string? InProcessTable { get; set; } = "EMP_IN_PROCESS_G";
    public bool DeleteInProcessOnExit { get; set; } = true;
    public string ProgramName { get; set; } = "OLL302";
    public string DefaultShift { get; set; } = "0";
    public string? DefaultDepartment { get; set; } = "0";
    public int CommandTimeoutSeconds { get; set; } = 90;
    public bool StopSyncOnMirrorFailure { get; set; }
    public bool SimulationMode { get; set; } = true;
    public bool OnlyCurrentDayWrites { get; set; } = true;
    public bool CloseOnlyWithFinalOutMode { get; set; } = true;
    public int MinimumOutGapMinutes { get; set; } = 1;
    public int MaxRowsPerRun { get; set; } = 200;
}
