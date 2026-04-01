using Checador.Application;
using Checador.Domain.Interfaces;
using Checador.Infrastructure.Access;
using Checador.Infrastructure.Excel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace Checador.Desktop;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "CHECADOR_")
            .Build();

        var appConfig = configuration.Get<AppConfiguration>() ?? new AppConfiguration();

        var startupAccessOptions = new AccessConnectionOptions
        {
            ConnectionString = appConfig.Access.ConnectionString,
            CommandTimeoutSeconds = appConfig.Access.CommandTimeoutSeconds
        };

        var startupDeviceOptions = new DirectDeviceOptions
        {
            Enabled = appConfig.Device.Enabled,
            PreferDirect = appConfig.Device.PreferDirect,
            FallbackToAccess = appConfig.Device.FallbackToAccess,
            Ip = appConfig.Device.Ip,
            Port = appConfig.Device.Port,
            CommKey = appConfig.Device.CommKey,
            MachineNumber = appConfig.Device.MachineNumber
        };

        var startupSqlOptions = new SqlSyncOptions
        {
            ConnectionString = appConfig.Sql.ConnectionString,
            InitialLookbackDays = appConfig.Sql.InitialLookbackDays,
            AutoSyncMinutes = appConfig.Sql.AutoSyncMinutes,
            ForceLookbackWindow = appConfig.Sql.ForceLookbackWindow
        };

        var startupDataSourceOptions = new DataSourceOptions
        {
            Primary = appConfig.DataSource.Primary
        };

        var startupPervasiveOptions = new PervasiveSyncOptions
        {
            Enabled = appConfig.PervasiveSync.Enabled,
            ConnectionString = appConfig.PervasiveSync.ConnectionString,
            AttendanceTable = appConfig.PervasiveSync.AttendanceTable,
            InProcessTable = appConfig.PervasiveSync.InProcessTable,
            DeleteInProcessOnExit = appConfig.PervasiveSync.DeleteInProcessOnExit,
            ProgramName = appConfig.PervasiveSync.ProgramName,
            DefaultShift = appConfig.PervasiveSync.DefaultShift,
            DefaultDepartment = appConfig.PervasiveSync.DefaultDepartment,
            CommandTimeoutSeconds = appConfig.PervasiveSync.CommandTimeoutSeconds,
            StopSyncOnMirrorFailure = appConfig.PervasiveSync.StopSyncOnMirrorFailure,
            SimulationMode = appConfig.PervasiveSync.SimulationMode,
            OnlyCurrentDayWrites = appConfig.PervasiveSync.OnlyCurrentDayWrites,
            CloseOnlyWithFinalOutMode = appConfig.PervasiveSync.CloseOnlyWithFinalOutMode,
            MinimumOutGapMinutes = appConfig.PervasiveSync.MinimumOutGapMinutes,
            MaxRowsPerRun = appConfig.PervasiveSync.MaxRowsPerRun
        };

        var startupValidator = new StartupRequirementsValidator(startupAccessOptions, startupDeviceOptions, startupSqlOptions, startupDataSourceOptions, startupPervasiveOptions);
        var startupErrors = startupValidator.Validate(appConfig.Report.OutputDirectory);
        if (startupErrors.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("No se puede arrancar el programa porque faltan requisitos:");
            sb.AppendLine();
            foreach (var error in startupErrors)
            {
                sb.AppendLine($"- {error}");
            }

            MessageBox.Show(
                sb.ToString(),
                "Error de requisitos",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        var services = new ServiceCollection();
        services.AddSingleton(appConfig);

        services.AddSingleton(startupAccessOptions);
        services.AddSingleton(new DirectDeviceOptions
        {
            Enabled = startupDeviceOptions.Enabled,
            PreferDirect = startupDeviceOptions.PreferDirect,
            FallbackToAccess = startupDeviceOptions.FallbackToAccess,
            Ip = startupDeviceOptions.Ip,
            Port = startupDeviceOptions.Port,
            CommKey = startupDeviceOptions.CommKey,
            MachineNumber = startupDeviceOptions.MachineNumber
        });

        services.AddSingleton(startupSqlOptions);

        services.AddSingleton(startupDataSourceOptions);
        services.AddSingleton(startupPervasiveOptions);

        services.AddSingleton(new AttendanceProcessingOptions
        {
            StandardWorkDayHours = appConfig.Processing.StandardWorkDayHours,
            RoundToMinutes = appConfig.Processing.RoundToMinutes,
            UseStrictFourPunchPolicy = appConfig.Processing.UseStrictFourPunchPolicy,
            EnableNightShiftSupport = appConfig.Processing.EnableNightShiftSupport,
            NightShiftScheduleCode = appConfig.Processing.NightShiftScheduleCode,
            NightShiftStartHour = appConfig.Processing.NightShiftStartHour,
            NightShiftRegularEndHour = appConfig.Processing.NightShiftRegularEndHour,
            NightShiftCarryToPreviousDayUntilHour = appConfig.Processing.NightShiftCarryToPreviousDayUntilHour,
            NightShiftStandardHours = appConfig.Processing.NightShiftStandardHours,
            Schedule1ExtraCountFromHour = appConfig.Processing.Schedule1ExtraCountFromHour,
            Schedule2ExtraCountFromHour = appConfig.Processing.Schedule2ExtraCountFromHour,
            Schedule3ExtraCountFromHour = appConfig.Processing.Schedule3ExtraCountFromHour
        });

        services.AddSingleton(new ExcelReportOptions
        {
            DefaultFileNamePattern = appConfig.Report.DefaultFileNamePattern,
            DataStartRow = appConfig.Report.DataStartRow,
            ForceWriteHeaders = appConfig.Report.ForceWriteHeaders
        });

        services.AddSingleton<AccessAttendanceDataSource>();
        services.AddSingleton<ZkTecoDirectAttendanceDataSource>();
        services.AddSingleton<SqlDeviceSyncService>();
        services.AddSingleton<PervasiveAttendanceMirrorService>();
        services.AddSingleton<SqlAttendanceDataSource>();
        services.AddSingleton<IAttendanceDataSource, AttendanceDataSourceRouter>();
        services.AddSingleton<IAttendanceDiagnostics, AccessDiagnosticsService>();
        services.AddSingleton<IReportGenerator, ClosedXmlReportGenerator>();
        services.AddSingleton<AttendanceProcessor>();
        services.AddSingleton<AttendanceReportOrchestrator>();
        services.AddTransient<Form1>();

        using var provider = services.BuildServiceProvider();
        var form = provider.GetRequiredService<Form1>();
        System.Windows.Forms.Application.Run(form);
    }
}
