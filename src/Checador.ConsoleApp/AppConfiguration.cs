namespace Checador.ConsoleApp;

public sealed class AppConfiguration
{
    public AccessSection Access { get; set; } = new();
    public DeviceSection Device { get; set; } = new();
    public DataSourceSection DataSource { get; set; } = new();
    public SqlSection Sql { get; set; } = new();
    public PervasiveSyncSection PervasiveSync { get; set; } = new();
    public ProcessingSection Processing { get; set; } = new();
    public ReportSection Report { get; set; } = new();

    public sealed class AccessSection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int CommandTimeoutSeconds { get; set; } = 120;
    }

    public sealed class ProcessingSection
    {
        public decimal StandardWorkDayHours { get; set; } = 8m;
        public int RoundToMinutes { get; set; } = 5;
        public bool UseStrictFourPunchPolicy { get; set; } = false;
        public bool EnableNightShiftSupport { get; set; } = true;
        public int NightShiftScheduleCode { get; set; } = 3;
        public int NightShiftStartHour { get; set; } = 18;
        public int NightShiftRegularEndHour { get; set; } = 7;
        public int NightShiftCarryToPreviousDayUntilHour { get; set; } = 12;
        public decimal NightShiftStandardHours { get; set; } = 9.60m;
        public int Schedule1ExtraCountFromHour { get; set; } = 7;
        public int Schedule2ExtraCountFromHour { get; set; } = 8;
        public int Schedule3ExtraCountFromHour { get; set; } = 18;
        public decimal Schedule1ExtraMaxHours { get; set; } = 2.4m;
        public decimal Schedule2ExtraMaxHours { get; set; } = 2.4m;
        public decimal Schedule3ExtraMaxHours { get; set; } = 3.6m;
    }

    public sealed class DeviceSection
    {
        public bool Enabled { get; set; } = true;
        public bool PreferDirect { get; set; } = true;
        public bool FallbackToAccess { get; set; } = true;
        public bool EnforceSyncAgentOnly { get; set; } = true;
        public List<string> AllowedEntryAssemblies { get; set; } = new() { "Checador.SyncAgent" };
        public string Ip { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 4370;
        public int CommKey { get; set; } = 0;
        public int MachineNumber { get; set; } = 1;
        public List<DeviceItemSection> Devices { get; set; } = new();
    }

    public sealed class DeviceItemSection
    {
        public string Label { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; } = 4370;
        public int CommKey { get; set; } = 0;
        public int MachineNumber { get; set; } = 2;
    }

    public sealed class SqlSection
    {
        public string ConnectionString { get; set; } = "Server=.;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;";
        public int InitialLookbackDays { get; set; } = 365;
        public int AutoSyncMinutes { get; set; } = 15;
    }

    public sealed class DataSourceSection
    {
        public string Primary { get; set; } = "Sql";
    }

    public sealed class PervasiveSyncSection
    {
        public bool Enabled { get; set; } = false;
        public string ConnectionString { get; set; } = string.Empty;
        public string AttendanceTable { get; set; } = "TIME_ATTENDANCE";
        public string InProcessTable { get; set; } = "EMP_IN_PROCESS_G";
        public bool DeleteInProcessOnExit { get; set; } = true;
        public string ProgramName { get; set; } = "OLL302";
        public string DefaultShift { get; set; } = "0";
        public string? DefaultDepartment { get; set; } = "0";
        public int CommandTimeoutSeconds { get; set; } = 20;
        public bool StopSyncOnMirrorFailure { get; set; } = false;
        public bool SimulationMode { get; set; } = true;
        public bool OnlyCurrentDayWrites { get; set; } = true;
        public bool CloseOnlyWithFinalOutMode { get; set; } = true;
        public int MinimumOutGapMinutes { get; set; } = 1;
        public int MaxRowsPerRun { get; set; } = 200;
    }

    public sealed class ReportSection
    {
        public string OutputDirectory { get; set; } = "reports";
        public string? TemplatePath { get; set; }
        public string DefaultFileNamePattern { get; set; } = "{Empleado}_{Semana}.xlsx";
        public int DataStartRow { get; set; } = 2;
        public bool ForceWriteHeaders { get; set; } = false;
    }
}
