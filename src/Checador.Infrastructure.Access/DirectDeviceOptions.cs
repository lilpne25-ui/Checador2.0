using System.Diagnostics;
using System.Reflection;

namespace Checador.Infrastructure.Access;

public sealed class DirectDeviceOptions
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
    public List<DirectDeviceEndpointOptions> Devices { get; set; } = new();

    public IReadOnlyList<DirectDeviceEndpointOptions> GetDevices()
    {
        var configured = Devices
            .Where(d => !string.IsNullOrWhiteSpace(d.Ip))
            .Select(d => d.Normalize())
            .ToList();

        if (configured.Count > 0)
        {
            return configured;
        }

        return
        [
            new DirectDeviceEndpointOptions
            {
                Ip = Ip,
                Port = Port,
                CommKey = CommKey,
                MachineNumber = MachineNumber,
                Label = "Principal"
            }.Normalize()
        ];
    }

    public DirectDeviceEndpointOptions GetPrimaryDevice()
        => GetDevices().First();

    public IReadOnlyList<string> GetAllowedEntryAssemblies()
    {
        var configured = AllowedEntryAssemblies
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (configured.Count > 0)
        {
            return configured;
        }

        return new[] { "Checador.SyncAgent" };
    }

    public bool IsCurrentProcessAuthorized()
    {
        if (!EnforceSyncAgentOnly)
        {
            return true;
        }

        var allowed = GetAllowedEntryAssemblies();
        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name?.Trim() ?? string.Empty;
        var processName = Process.GetCurrentProcess().ProcessName?.Trim() ?? string.Empty;

        return allowed.Any(x =>
            string.Equals(x, entryAssemblyName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x, processName, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class DirectDeviceEndpointOptions
{
    public string Label { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public int Port { get; set; } = 4370;
    public int CommKey { get; set; } = 0;
    public int MachineNumber { get; set; } = 2;

    public DirectDeviceEndpointOptions Normalize()
    {
        if (Port <= 0)
        {
            Port = 4370;
        }

        if (MachineNumber <= 0)
        {
            MachineNumber = 2;
        }

        Label = Label?.Trim() ?? string.Empty;
        Ip = Ip?.Trim() ?? string.Empty;
        return this;
    }
}
