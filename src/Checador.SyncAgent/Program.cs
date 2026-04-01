using Checador.Infrastructure.Access;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Checador.SyncAgent;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "CHECADOR_")
            .Build();

        var cfg = configuration.Get<SyncAgentConfiguration>() ?? new SyncAgentConfiguration();

        var services = new ServiceCollection();
        services.AddSingleton(new DirectDeviceOptions
        {
            Enabled = cfg.Device.Enabled,
            PreferDirect = cfg.Device.PreferDirect,
            FallbackToAccess = cfg.Device.FallbackToAccess,
            Ip = cfg.Device.Ip,
            Port = cfg.Device.Port,
            CommKey = cfg.Device.CommKey,
            MachineNumber = cfg.Device.MachineNumber
        });

        services.AddSingleton(new SqlSyncOptions
        {
            ConnectionString = cfg.Sql.ConnectionString,
            InitialLookbackDays = cfg.Sql.InitialLookbackDays,
            AutoSyncMinutes = cfg.Sql.AutoSyncMinutes,
            ForceLookbackWindow = cfg.Sql.ForceLookbackWindow
        });

        services.AddSingleton(new PervasiveSyncOptions
        {
            Enabled = cfg.PervasiveSync.Enabled,
            ConnectionString = cfg.PervasiveSync.ConnectionString,
            AttendanceTable = cfg.PervasiveSync.AttendanceTable,
            InProcessTable = cfg.PervasiveSync.InProcessTable,
            DeleteInProcessOnExit = cfg.PervasiveSync.DeleteInProcessOnExit,
            ProgramName = cfg.PervasiveSync.ProgramName,
            DefaultShift = cfg.PervasiveSync.DefaultShift,
            DefaultDepartment = cfg.PervasiveSync.DefaultDepartment,
            CommandTimeoutSeconds = cfg.PervasiveSync.CommandTimeoutSeconds,
            StopSyncOnMirrorFailure = cfg.PervasiveSync.StopSyncOnMirrorFailure,
            SimulationMode = cfg.PervasiveSync.SimulationMode,
            OnlyCurrentDayWrites = cfg.PervasiveSync.OnlyCurrentDayWrites,
            CloseOnlyWithFinalOutMode = cfg.PervasiveSync.CloseOnlyWithFinalOutMode,
            MinimumOutGapMinutes = cfg.PervasiveSync.MinimumOutGapMinutes,
            MaxRowsPerRun = cfg.PervasiveSync.MaxRowsPerRun
        });

        services.AddSingleton<ZkTecoDirectAttendanceDataSource>();
        services.AddSingleton<PervasiveAttendanceMirrorService>();
        services.AddSingleton<SqlDeviceSyncService>();
        services.AddTransient<SyncAgentForm>();

        using var provider = services.BuildServiceProvider();
        var form = provider.GetRequiredService<SyncAgentForm>();
        Application.Run(form);
    }
}
