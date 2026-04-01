using System.Data.OleDb;
using System.Data.Odbc;
using Microsoft.Data.SqlClient;

namespace Checador.Infrastructure.Access;

public sealed class StartupRequirementsValidator
{
    private readonly AccessConnectionOptions _accessOptions;
    private readonly DirectDeviceOptions? _deviceOptions;
    private readonly SqlSyncOptions? _sqlOptions;
    private readonly DataSourceOptions? _dataSourceOptions;
    private readonly PervasiveSyncOptions? _pervasiveOptions;

    public StartupRequirementsValidator(
        AccessConnectionOptions accessOptions,
        DirectDeviceOptions? deviceOptions = null,
        SqlSyncOptions? sqlOptions = null,
        DataSourceOptions? dataSourceOptions = null,
        PervasiveSyncOptions? pervasiveOptions = null)
    {
        _accessOptions = accessOptions;
        _deviceOptions = deviceOptions;
        _sqlOptions = sqlOptions;
        _dataSourceOptions = dataSourceOptions;
        _pervasiveOptions = pervasiveOptions;
    }

    public IReadOnlyList<string> Validate(string outputDirectory)
    {
        var errors = new List<string>();
        var requiresAccess = RequiresAccessChecks();

        ValidateDotnetRuntime(errors);
        ValidateDirectSdk(errors);
        ValidateSqlIfRequired(errors);
        ValidatePervasiveIfEnabled(errors);

        if (requiresAccess)
        {
            ValidateAceProvider(errors);

            var mdbPath = ExtractDataSourcePath(_accessOptions.ConnectionString);
            ValidateMdbReadAccess(mdbPath, errors);
            ValidateOleDbConnection(errors);
        }

        ValidateOutputWriteAccess(outputDirectory, errors);

        return errors.Distinct().ToList();
    }

    private bool RequiresAccessChecks()
    {
        var primary = (_dataSourceOptions?.Primary ?? string.Empty).Trim();
        if (primary.Equals("Sql", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (primary.Equals("Access", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (_deviceOptions is null)
        {
            return true;
        }

        var directOnly = _deviceOptions.Enabled && _deviceOptions.PreferDirect && !_deviceOptions.FallbackToAccess;
        return !directOnly;
    }

    private void ValidateSqlIfRequired(List<string> errors)
    {
        var primary = (_dataSourceOptions?.Primary ?? string.Empty).Trim();
        if (!primary.Equals("Sql", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_sqlOptions is null || string.IsNullOrWhiteSpace(_sqlOptions.ConnectionString))
        {
            errors.Add("Modo SQL habilitado, pero falta Sql.ConnectionString.");
            return;
        }

        try
        {
            using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            connection.Open();
        }
        catch (Exception ex)
        {
            errors.Add($"Conexión SQL fallida para modo SQL primario. Detalle: {ex.Message}");
        }
    }

    private void ValidatePervasiveIfEnabled(List<string> errors)
    {
        if (_pervasiveOptions is null || !_pervasiveOptions.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_pervasiveOptions.ConnectionString))
        {
            errors.Add("PervasiveSync.Enabled=true, pero falta PervasiveSync.ConnectionString.");
            return;
        }

        try
        {
            using var connection = new OdbcConnection(_pervasiveOptions.ConnectionString);
            connection.Open();
        }
        catch (Exception ex)
        {
            errors.Add($"Conexión Pervasive fallida. Detalle: {ex.Message}");
        }
    }

    private void ValidateDirectSdk(List<string> errors)
    {
        if (_deviceOptions is null || !_deviceOptions.Enabled || !_deviceOptions.PreferDirect)
        {
            return;
        }

        var primary = (_dataSourceOptions?.Primary ?? string.Empty).Trim();
        if (primary.Equals("Sql", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var zkem = Type.GetTypeFromProgID("zkemkeeper.CZKEM");
        zkem ??= Type.GetTypeFromProgID("zkemkeeper.ZKEM");
        zkem ??= Type.GetTypeFromProgID("zkemkeeper.ZKEM.1");
        if (zkem is null)
        {
            errors.Add("Modo directo habilitado, pero falta SDK COM ZKTeco (zkemkeeper.ZKEM/CZKEM). Ejecuta instalar-zkteco-sdk-admin.ps1 como Administrador.");
            return;
        }

        try
        {
            var obj = Activator.CreateInstance(zkem);
            if (obj is not null)
            {
                try
                {
                    dynamic dyn = obj;
                    dyn.Disconnect();
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Modo directo habilitado, pero COM ZKTeco no está completamente registrado (CLSID/Inproc). Detalle: {ex.Message}. Ejecuta instalar-zkteco-sdk-admin.ps1 como Administrador.");
        }
    }

    private static void ValidateDotnetRuntime(List<string> errors)
    {
        if (Environment.Version.Major < 8)
        {
            errors.Add($"1) .NET Runtime incompatible detectado ({Environment.Version}). Requiere .NET 8+.");
        }
    }

    private static void ValidateAceProvider(List<string> errors)
    {
        var ace12 = Type.GetTypeFromProgID("Microsoft.ACE.OLEDB.12.0");
        var ace16 = Type.GetTypeFromProgID("Microsoft.ACE.OLEDB.16.0");

        if (ace12 is null && ace16 is null)
        {
            errors.Add("2) No se detecta Microsoft Access Database Engine (ACE OLEDB 12.0 o 16.0).");
        }
    }

    private static void ValidateMdbReadAccess(string? mdbPath, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(mdbPath))
        {
            errors.Add("3) No se pudo extraer 'Data Source' de Access.ConnectionString.");
            return;
        }

        if (!File.Exists(mdbPath))
        {
            errors.Add($"3) No existe el archivo MDB o no es accesible: {mdbPath}");
            return;
        }

        try
        {
            using var fs = new FileStream(mdbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            _ = fs.Length;
        }
        catch (UnauthorizedAccessException ex)
        {
            errors.Add($"3) Sin permisos de lectura sobre MDB ({mdbPath}). Detalle: {ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"3) Error al leer MDB ({mdbPath}). Detalle: {ex.Message}");
        }
    }

    private static void ValidateOutputWriteAccess(string outputDirectory, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            errors.Add("4) Report.OutputDirectory está vacío.");
            return;
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
            var probeFile = Path.Combine(outputDirectory, $".perm_probe_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probeFile, "ok");
            File.Delete(probeFile);
        }
        catch (UnauthorizedAccessException ex)
        {
            errors.Add($"4) Sin permisos de escritura en carpeta de reportes ({outputDirectory}). Detalle: {ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"4) Error al validar escritura en carpeta de reportes ({outputDirectory}). Detalle: {ex.Message}");
        }
    }

    private void ValidateOleDbConnection(List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(_accessOptions.ConnectionString))
        {
            errors.Add("Access.ConnectionString está vacío.");
            return;
        }

        try
        {
            using var connection = new OleDbConnection(_accessOptions.ConnectionString);
            connection.Open();
        }
        catch (Exception ex)
        {
            errors.Add($"Conexión OLEDB fallida hacia att2000.mdb. Detalle: {ex.Message}");
        }
    }

    private static string? ExtractDataSourcePath(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var idx = part.IndexOf('=');
            if (idx <= 0) continue;

            var key = part[..idx].Trim();
            var value = part[(idx + 1)..].Trim().Trim('"');
            if (key.Equals("Data Source", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }
}
