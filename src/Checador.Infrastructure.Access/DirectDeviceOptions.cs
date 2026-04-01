namespace Checador.Infrastructure.Access;

public sealed class DirectDeviceOptions
{
    public bool Enabled { get; set; } = true;
    public bool PreferDirect { get; set; } = true;
    public bool FallbackToAccess { get; set; } = true;
    public string Ip { get; set; } = "192.168.1.201";
    public int Port { get; set; } = 4370;
    public int CommKey { get; set; } = 0;
    public int MachineNumber { get; set; } = 2;
}
