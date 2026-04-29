namespace Checador.SyncAgent;

public sealed class SyncAgentConfiguration
{
    public DeviceSection Device { get; set; } = new();
    public SqlSection Sql { get; set; } = new();
    public PervasiveSyncSection PervasiveSync { get; set; } = new();
    public AutoScheduleSection AutoSchedule { get; set; } = new();

    public sealed class DeviceSection
    {
        public bool Enabled { get; set; } = true;
        public bool PreferDirect { get; set; } = true;
        public bool FallbackToAccess { get; set; } = false;
        public string Ip { get; set; } = "192.168.1.201";
        public int Port { get; set; } = 4370;
        public int CommKey { get; set; } = 0;
        public int MachineNumber { get; set; } = 2;
    }

    public sealed class SqlSection
    {
        public string ConnectionString { get; set; } = "Server=.;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;";
        public int InitialLookbackDays { get; set; } = 365;
        public int AutoSyncMinutes { get; set; } = 15;
        public bool ForceLookbackWindow { get; set; } = false;
    }

    public sealed class PervasiveSyncSection
    {
        public bool Enabled { get; set; } = false;
        public string ConnectionString { get; set; } = "Driver={Pervasive ODBC Unicode Interface};ServerName=OCISERVER;DBQ=GLOBALOCI;UID=GLOBALOCI;PWD=GLOBALOCI;";
        public string AttendanceTable { get; set; } = "TIME_ATTENDANCE";
        public string InProcessTable { get; set; } = "EMP_IN_PROCESS_G";
        public bool DeleteInProcessOnExit { get; set; } = true;
        public string ProgramName { get; set; } = "OLL302";
        public string DefaultShift { get; set; } = "0";
        public string? DefaultDepartment { get; set; } = "0";
        public int CommandTimeoutSeconds { get; set; } = 90;
        public bool StopSyncOnMirrorFailure { get; set; } = false;
        public bool SimulationMode { get; set; } = true;
        public bool OnlyCurrentDayWrites { get; set; } = true;
        public bool CloseOnlyWithFinalOutMode { get; set; } = true;
        public int MinimumOutGapMinutes { get; set; } = 1;
        public int MaxRowsPerRun { get; set; } = 200;
    }

    public sealed class AutoScheduleSection
    {
        public bool Enabled { get; set; } = true;
        public int IntervalMinutes { get; set; } = 10;
        public int PollSeconds { get; set; } = 30;
        public List<TimeWindowSection> Windows { get; set; } =
        [
            new TimeWindowSection { Start = "05:00", End = "09:00" },
            new TimeWindowSection { Start = "11:00", End = "03:30" },
            new TimeWindowSection { Start = "16:00", End = "22:00" }
        ];
    }

    public sealed class TimeWindowSection
    {
        public string Start { get; set; } = "00:00";
        public string End { get; set; } = "23:59";
    }
}
