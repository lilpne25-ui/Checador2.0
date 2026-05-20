using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;
using Checador.Domain.Interfaces;
using Checador.Domain.Models;
using Microsoft.CSharp.RuntimeBinder;

namespace Checador.Infrastructure.Access;

public sealed class ZkTecoDirectAttendanceDataSource : IAttendanceDataSource
{
    private readonly DirectDeviceOptions _options;
    private static readonly object GuardLogLock = new();

    public ZkTecoDirectAttendanceDataSource(DirectDeviceOptions options)
    {
        _options = options;
    }

    public Task<IReadOnlyList<PunchRecord>> GetPunchesAsync(int? userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var devices = _options.GetDevices();
        var allPunches = new List<PunchRecord>();
        var nameByUser = new Dictionary<int, string>();
        var errors = new List<string>();

        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object? zkem = null;
            try
            {
                zkem = Connect(device);

                var deviceEmployees = ReadEmployees(zkem, device.MachineNumber);
                foreach (var employee in deviceEmployees)
                {
                    nameByUser[employee.UserId] = employee.EmployeeName;
                }

                var punches = ReadPunches(zkem, device.MachineNumber, from, to, nameByUser);
                allPunches.AddRange(punches);
            }
            catch (Exception ex)
            {
                var label = string.IsNullOrWhiteSpace(device.Label) ? device.Ip : device.Label;
                errors.Add($"{label} ({device.Ip}:{device.Port}): {ex.Message}");
            }
            finally
            {
                if (zkem is not null)
                {
                    SafeDisconnect(zkem);
                }
            }
        }

        if (allPunches.Count == 0 && errors.Count > 0)
        {
            throw new InvalidOperationException(
                "No se pudo obtener marcas de ningun dispositivo. " + string.Join(" | ", errors));
        }

        var dedupedPunches = allPunches
            .GroupBy(p => (p.UserId, p.CheckTime, p.InOutMode))
            .Select(g => g.First())
            .OrderBy(p => p.UserId)
            .ThenBy(p => p.CheckTime)
            .ToList();

        if (userId.HasValue)
        {
            dedupedPunches = dedupedPunches.Where(p => p.UserId == userId.Value).ToList();
        }

        return Task.FromResult<IReadOnlyList<PunchRecord>>(dedupedPunches);
    }

    public Task<IReadOnlyList<EmployeeInfo>> GetEmployeesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var devices = _options.GetDevices();
        var employees = new List<EmployeeInfo>();
        var errors = new List<string>();

        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object? zkem = null;
            try
            {
                zkem = Connect(device);
                employees.AddRange(ReadEmployees(zkem, device.MachineNumber));
            }
            catch (Exception ex)
            {
                var label = string.IsNullOrWhiteSpace(device.Label) ? device.Ip : device.Label;
                errors.Add($"{label} ({device.Ip}:{device.Port}): {ex.Message}");
            }
            finally
            {
                if (zkem is not null)
                {
                    SafeDisconnect(zkem);
                }
            }
        }

        if (employees.Count == 0 && errors.Count > 0)
        {
            throw new InvalidOperationException(
                "No se pudo leer catalogo de ningun dispositivo. " + string.Join(" | ", errors));
        }

        var deduped = employees
            .GroupBy(e => e.UserId)
            .Select(g => g.First())
            .OrderBy(e => e.UserId)
            .ToList();

        return Task.FromResult<IReadOnlyList<EmployeeInfo>>(deduped);
    }

    public Task<(bool Success, string Message)> TryUpdateEmployeeNameAsync(int userId, string newName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId <= 0)
        {
            return Task.FromResult((false, "USERID inválido."));
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            return Task.FromResult((false, "El nombre no puede ir vacío."));
        }

        var primaryDevice = _options.GetPrimaryDevice();
        var zkem = Connect(primaryDevice);
        try
        {
            dynamic sdk = zkem;
            var machine = primaryDevice.MachineNumber;
            var enroll = userId.ToString();

            try
            {
                bool okSsr = sdk.SSR_SetUserInfo(machine, enroll, newName.Trim(), string.Empty, 0, true);
                if (okSsr)
                {
                    return Task.FromResult((true, "Nombre actualizado en reloj (SSR_SetUserInfo)."));
                }
            }
            catch
            {
            }

            try
            {
                bool okLegacy = sdk.SetUserInfo(machine, userId, newName.Trim(), string.Empty, 0, true);
                if (okLegacy)
                {
                    return Task.FromResult((true, "Nombre actualizado en reloj (SetUserInfo)."));
                }
            }
            catch
            {
            }

            return Task.FromResult((false, "El SDK no confirmó actualización de nombre en el dispositivo."));
        }
        catch (Exception ex)
        {
            return Task.FromResult((false, $"Error al actualizar nombre en reloj: {ex.Message}"));
        }
        finally
        {
            SafeDisconnect(zkem);
        }
    }

    public Task<(bool Supported, string Message)> ProbePhotoCapabilityAsync(int userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId <= 0)
        {
            return Task.FromResult((false, "USERID inválido para consultar foto."));
        }

        var primaryDevice = _options.GetPrimaryDevice();
        var zkem = Connect(primaryDevice);
        try
        {
            var methods = zkem.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Select(m => m.Name)
                .Where(n => n.Contains("Photo", StringComparison.OrdinalIgnoreCase)
                         || n.Contains("Face", StringComparison.OrdinalIgnoreCase)
                         || n.Contains("UserFace", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();

            if (methods.Count == 0)
            {
                return Task.FromResult((false,
                    "Este SDK/firmware no expone métodos de foto/facial para extracción directa de imagen."));
            }

            var methodsText = string.Join(", ", methods.Take(12));
            return Task.FromResult((false,
                "Se detectaron métodos faciales en SDK, pero no hay API estable de exportación de foto en este flujo. Métodos detectados: " + methodsText));
        }
        catch (Exception ex)
        {
            return Task.FromResult((false, $"No fue posible consultar capacidades de foto del SDK. Detalle: {ex.Message}"));
        }
        finally
        {
            SafeDisconnect(zkem);
        }
    }

    public Task<(bool Success, byte[]? PhotoBytes, string? ContentType, string Message)> TryGetEmployeePhotoAsync(int userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId <= 0)
        {
            return Task.FromResult((false, (byte[]?)null, (string?)null, "USERID inválido para consultar foto."));
        }

        var primaryDevice = _options.GetPrimaryDevice();
        var zkem = Connect(primaryDevice);
        try
        {
            dynamic sdk = zkem;
            var machine = primaryDevice.MachineNumber;
            var enroll = userId.ToString();

            string? payload = null;
            var methodsTried = new List<string>();

            if (TryReadFacePayload((object)sdk, machine, enroll, methodsTried, out payload))
            {
                if (TryDecodeImagePayload(payload, out var bytes, out var contentType))
                {
                    return Task.FromResult((true, (byte[]?)bytes, (string?)contentType, "Foto obtenida desde el dispositivo por SDK."));
                }

                return Task.FromResult((false, (byte[]?)null, (string?)null,
                    "El reloj devolvió datos faciales, pero no en formato de imagen exportable por este SDK."));
            }

            var methods = zkem.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Select(m => m.Name)
                .Where(n => n.Contains("Photo", StringComparison.OrdinalIgnoreCase)
                         || n.Contains("Face", StringComparison.OrdinalIgnoreCase)
                         || n.Contains("UserFace", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .Take(12)
                .ToList();

            var tried = methodsTried.Count > 0 ? $" Intentos: {string.Join(", ", methodsTried.Distinct(StringComparer.OrdinalIgnoreCase))}." : string.Empty;
            var available = methods.Count > 0 ? $" Métodos detectados: {string.Join(", ", methods)}." : "";

            return Task.FromResult((false, (byte[]?)null, (string?)null,
                "No se pudo extraer foto del checador con las APIs disponibles del SDK." + tried + available));
        }
        catch (Exception ex)
        {
            return Task.FromResult((false, (byte[]?)null, (string?)null, $"Error consultando foto en reloj: {ex.Message}"));
        }
        finally
        {
            SafeDisconnect(zkem);
        }
    }

    public Task<(bool Success, string Message)> TrySyncDeviceTimeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var primaryDevice = _options.GetPrimaryDevice();
        var zkem = Connect(primaryDevice);
        try
        {
            dynamic sdk = zkem;
            var machine = primaryDevice.MachineNumber;
            var now = DateTime.Now;

            try
            {
                bool ok = sdk.SetDeviceTime(machine);
                if (ok)
                {
                    return Task.FromResult((true, $"Hora del reloj sincronizada con hora local ({now:dd/MM/yyyy HH:mm:ss}) usando SetDeviceTime."));
                }
            }
            catch
            {
            }

            try
            {
                bool ok2 = sdk.SetDeviceTime2(machine, now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
                if (ok2)
                {
                    return Task.FromResult((true, $"Hora del reloj ajustada ({now:dd/MM/yyyy HH:mm:ss}) usando SetDeviceTime2."));
                }
            }
            catch
            {
            }

            return Task.FromResult((false, "El SDK no confirmó cambio de hora en el dispositivo."));
        }
        catch (Exception ex)
        {
            return Task.FromResult((false, $"Error al sincronizar hora del reloj: {ex.Message}"));
        }
        finally
        {
            SafeDisconnect(zkem);
        }
    }

    public Task<(bool Connected, DateTime? DeviceTime, int? DeviceUsers, string Message)> TryGetLiveStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object zkem;
        try
        {
            zkem = Connect(_options.GetPrimaryDevice());
        }
        catch (Exception ex)
        {
            return Task.FromResult((false, (DateTime?)null, (int?)null, $"Sin conexión al reloj: {ex.Message}"));
        }

        try
        {
            var machine = _options.GetPrimaryDevice().MachineNumber;

            DateTime? deviceTime = null;
            var timeDetail = string.Empty;
            if (!TryReadDeviceTime(zkem, machine, out deviceTime, out timeDetail))
            {
                deviceTime = null;
            }

            int? deviceUsers = null;
            try
            {
                deviceUsers = ReadEmployees(zkem, machine).Count;
            }
            catch
            {
                deviceUsers = null;
            }

            var msg = "Conexión al reloj OK.";
            if (deviceTime is null)
            {
                msg += " Hora del reloj no disponible por SDK.";
                if (!string.IsNullOrWhiteSpace(timeDetail))
                {
                    msg += " " + timeDetail;
                }
            }
            else
            {
                msg += $" Hora del reloj: {deviceTime:dd/MM/yyyy HH:mm:ss}.";
            }

            return Task.FromResult((true, deviceTime, deviceUsers, msg));
        }
        catch (Exception ex)
        {
            return Task.FromResult((false, (DateTime?)null, (int?)null, $"Error leyendo estado en vivo: {ex.Message}"));
        }
        finally
        {
            SafeDisconnect(zkem);
        }
    }

    public Task<(bool Success, string Message)> TryDeleteEmployeeAsync(int userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId <= 0)
        {
            return Task.FromResult((false, "USERID inválido para eliminar."));
        }

        var primaryDevice = _options.GetPrimaryDevice();
        var zkem = Connect(primaryDevice);
        try
        {
            dynamic sdk = zkem;
            var machine = primaryDevice.MachineNumber;
            var enroll = userId.ToString();

            try
            {
                bool okSsr = sdk.SSR_DeleteEnrollData(machine, enroll, 12);
                if (okSsr)
                {
                    return Task.FromResult((true, "Empleado eliminado en reloj (SSR_DeleteEnrollData)."));
                }
            }
            catch
            {
            }

            try
            {
                bool okLegacy = sdk.DeleteEnrollData(machine, userId, 12);
                if (okLegacy)
                {
                    return Task.FromResult((true, "Empleado eliminado en reloj (DeleteEnrollData)."));
                }
            }
            catch
            {
            }

            return Task.FromResult((false, "El SDK no confirmó eliminación en el dispositivo."));
        }
        catch (Exception ex)
        {
            return Task.FromResult((false, $"Error al eliminar empleado en reloj: {ex.Message}"));
        }
        finally
        {
            SafeDisconnect(zkem);
        }
    }

    public bool IsCurrentProcessAuthorizedForDirectDevice()
    {
        if (!_options.EnforceSyncAgentOnly)
        {
            return true;
        }

        return _options.IsCurrentProcessAuthorized();
    }

    public string BuildDirectAccessBlockedMessage(string operation = "Connect")
    {
        var primaryDevice = _options.GetPrimaryDevice();
        return BuildGuardMessage(operation, primaryDevice);
    }

    private object Connect(DirectDeviceEndpointOptions device)
    {
        GuardDirectAccessOrThrow("Connect", device);

        var type = Type.GetTypeFromProgID("zkemkeeper.CZKEM")
                   ?? Type.GetTypeFromProgID("zkemkeeper.ZKEM")
                   ?? Type.GetTypeFromProgID("zkemkeeper.ZKEM.1");
        if (type is null)
        {
            throw new InvalidOperationException("No se encontró COM ZKTeco (zkemkeeper.ZKEM/CZKEM). Instala o registra el SDK StandAlone y ejecuta la app en x86.", new COMException());
        }

        dynamic zkem = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("No se pudo crear instancia COM de zkemkeeper.CZKEM.");

        try
        {
            if (device.CommKey > 0)
            {
                try
                {
                    zkem.SetCommPassword(device.CommKey);
                }
                catch
                {
                    // Algunos firmwares ignoran este método o requieren orden distinto.
                }
            }

            bool connected = zkem.Connect_Net(device.Ip, device.Port);
            if (!connected)
            {
                throw new InvalidOperationException($"No se pudo conectar al checador {device.Ip}:{device.Port} por SDK ZKTeco.");
            }

            return zkem;
        }
        catch
        {
            SafeDisconnect(zkem);
            throw;
        }
    }

    private void GuardDirectAccessOrThrow(string operation, DirectDeviceEndpointOptions device)
    {
        if (IsCurrentProcessAuthorizedForDirectDevice())
        {
            return;
        }

        var message = BuildGuardMessage(operation, device);

        WriteGuardLog(message);
        throw new InvalidOperationException(message);
    }

    private string BuildGuardMessage(string operation, DirectDeviceEndpointOptions device)
    {
        var entryAssembly = Assembly.GetEntryAssembly()?.GetName().Name ?? "(unknown)";
        var processName = Process.GetCurrentProcess().ProcessName ?? "(unknown)";
        var allowed = string.Join(", ", _options.GetAllowedEntryAssemblies());
        if (string.IsNullOrWhiteSpace(allowed))
        {
            allowed = "Checador.SyncAgent";
        }

        return $"Acceso directo a ZKTeco bloqueado. Solo procesos autorizados pueden conectarse al reloj. Operation={operation}, EntryAssembly={entryAssembly}, Process={processName}, Device={device.Ip}:{device.Port}, Allowed={allowed}.";
    }

    private static void WriteGuardLog(string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDir);
            var logPath = Path.Combine(logsDir, "direct-device-guard.log");
            lock (GuardLogLock)
            {
                File.AppendAllText(logPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Guard log should never break app execution flow.
        }
    }

    private List<EmployeeInfo> ReadEmployees(object zkemObj, int machine)
    {
        dynamic zkem = zkemObj;
        var employees = new List<EmployeeInfo>();

        bool ready;
        try
        {
            ready = zkem.ReadAllUserID(machine);
        }
        catch (RuntimeBinderException ex)
        {
            throw new InvalidOperationException("El SDK no expone ReadAllUserID en este dispositivo/firmware.", ex);
        }

        if (!ready)
        {
            return employees;
        }

        while (true)
        {
            try
            {
                string enroll;
                string name;
                string password;
                int privilege;
                bool enabled;

                bool ok = zkem.SSR_GetAllUserInfo(machine, out enroll, out name, out password, out privilege, out enabled);
                if (!ok)
                {
                    break;
                }

                if (int.TryParse(enroll, out var userId))
                {
                    var employeeName = string.IsNullOrWhiteSpace(name) ? $"Empleado_{userId}" : name;
                    employees.Add(new EmployeeInfo(userId, employeeName));
                }
            }
            catch (RuntimeBinderException)
            {
                break;
            }
        }

        return employees
            .GroupBy(e => e.UserId)
            .Select(g => g.First())
            .OrderBy(e => e.UserId)
            .ToList();
    }

    private List<PunchRecord> ReadPunches(object zkemObj, int machine, DateTime from, DateTime to, Dictionary<int, string> names)
    {
        dynamic zkem = zkemObj;
        var list = new List<PunchRecord>();

        bool ready;
        try
        {
            ready = zkem.ReadGeneralLogData(machine);
        }
        catch (RuntimeBinderException ex)
        {
            throw new InvalidOperationException("El SDK no expone ReadGeneralLogData en este dispositivo/firmware.", ex);
        }

        if (!ready)
        {
            return list;
        }

        var readAny = TryReadSsrLogs(zkem, machine, from, to, names, list);
        if (!readAny)
        {
            TryReadGeneralLogDataStr(zkem, machine, from, to, names, list);
        }

        return list
            .OrderBy(p => p.UserId)
            .ThenBy(p => p.CheckTime)
            .ToList();
    }

    private static bool TryReadSsrLogs(object zkemObj, int machine, DateTime from, DateTime to, Dictionary<int, string> names, List<PunchRecord> target)
    {
        dynamic zkem = zkemObj;
        var success = false;

        while (true)
        {
            try
            {
                string enroll;
                int verifyMode;
                int inOutMode;
                int year;
                int month;
                int day;
                int hour;
                int minute;
                int second;
                int workCode;

                bool ok = zkem.SSR_GetGeneralLogData(
                    machine,
                    out enroll,
                    out verifyMode,
                    out inOutMode,
                    out year,
                    out month,
                    out day,
                    out hour,
                    out minute,
                    out second,
                    out workCode);

                if (!ok)
                {
                    break;
                }

                success = true;

                if (!int.TryParse(enroll, out var userId))
                {
                    continue;
                }

                var ts = new DateTime(year, month, day, hour, minute, second);
                if (ts < from || ts > to)
                {
                    continue;
                }

                var name = names.TryGetValue(userId, out var employeeName)
                    ? employeeName
                    : $"Empleado_{userId}";

                target.Add(new PunchRecord(userId, name, ts, inOutMode));
            }
            catch (RuntimeBinderException)
            {
                return false;
            }
            catch
            {
                break;
            }
        }

        return success;
    }

    private static bool TryReadGeneralLogDataStr(object zkemObj, int machine, DateTime from, DateTime to, Dictionary<int, string> names, List<PunchRecord> target)
    {
        dynamic zkem = zkemObj;
        var success = false;

        while (true)
        {
            try
            {
                string enroll;
                int verifyMode;
                int inOutMode;
                string dateTimeStr;
                int workCode;

                bool ok = zkem.GetGeneralLogDataStr(
                    machine,
                    out enroll,
                    out verifyMode,
                    out inOutMode,
                    out dateTimeStr,
                    out workCode);

                if (!ok)
                {
                    break;
                }

                success = true;

                if (!int.TryParse(enroll, out var userId))
                {
                    continue;
                }

                if (!DateTime.TryParse(dateTimeStr, out var ts))
                {
                    continue;
                }

                if (ts < from || ts > to)
                {
                    continue;
                }

                var name = names.TryGetValue(userId, out var employeeName)
                    ? employeeName
                    : $"Empleado_{userId}";

                target.Add(new PunchRecord(userId, name, ts, inOutMode));
            }
            catch (RuntimeBinderException)
            {
                return false;
            }
            catch
            {
                break;
            }
        }

        return success;
    }

    private static void SafeDisconnect(object zkemObj)
    {
        try
        {
            dynamic zkem = zkemObj;
            zkem.Disconnect();
        }
        catch
        {
        }
    }

    private static bool TryReadFacePayload(object sdkObj, int machine, string enroll, List<string> methodsTried, out string? payload)
    {
        dynamic sdk = sdkObj;
        payload = null;

        try
        {
            string faceData;
            int len;
            methodsTried.Add("GetUserFaceStr");
            bool ok = sdk.GetUserFaceStr(machine, enroll, out faceData, out len);
            if (ok && !string.IsNullOrWhiteSpace(faceData))
            {
                payload = faceData;
                return true;
            }
        }
        catch
        {
        }

        try
        {
            string faceData;
            int len;
            methodsTried.Add("SSR_GetUserFaceStr");
            bool ok = sdk.SSR_GetUserFaceStr(machine, enroll, out faceData, out len);
            if (ok && !string.IsNullOrWhiteSpace(faceData))
            {
                payload = faceData;
                return true;
            }
        }
        catch
        {
        }

        try
        {
            string faceData;
            methodsTried.Add("GetUserFacePhoto");
            bool ok = sdk.GetUserFacePhoto(machine, enroll, out faceData);
            if (ok && !string.IsNullOrWhiteSpace(faceData))
            {
                payload = faceData;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool TryDecodeImagePayload(string? payload, out byte[] bytes, out string contentType)
    {
        bytes = Array.Empty<byte>();
        contentType = "application/octet-stream";

        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var raw = payload.Trim();

        const string dataPrefix = "data:";
        if (raw.StartsWith(dataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var idx = raw.IndexOf(",", StringComparison.Ordinal);
            if (idx > 0)
            {
                var meta = raw[..idx];
                var body = raw[(idx + 1)..];

                var semi = meta.IndexOf(";", StringComparison.Ordinal);
                if (semi > dataPrefix.Length)
                {
                    contentType = meta[dataPrefix.Length..semi];
                }

                try
                {
                    bytes = Convert.FromBase64String(body);
                    return bytes.Length > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        try
        {
            bytes = Convert.FromBase64String(raw);
            if (bytes.Length == 0)
            {
                return false;
            }

            contentType = DetectImageType(bytes);
            return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string DetectImageType(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
        {
            return "image/bmp";
        }

        return "application/octet-stream";
    }

    private static bool TryReadDeviceTime(object sdkObj, int machine, out DateTime? deviceTime, out string detail)
    {
        deviceTime = null;
        detail = string.Empty;

        var sdkType = sdkObj.GetType();
        var methods = sdkType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name.Equals("GetDeviceTime2", StringComparison.OrdinalIgnoreCase)
                     || m.Name.Equals("GetDeviceTimeStr", StringComparison.OrdinalIgnoreCase)
                     || m.Name.Equals("GetDeviceTime", StringComparison.OrdinalIgnoreCase)
                     || m.Name.Equals("SSR_GetDeviceTime", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in methods.OrderBy(m => m.Name).ThenBy(m => m.GetParameters().Length))
        {
            try
            {
                if (TryInvokeTimeMethod(sdkObj, method, machine, out var parsed))
                {
                    deviceTime = parsed;
                    return true;
                }
            }
            catch
            {
                // intentar siguiente firma
            }
        }

        if (methods.Count == 0)
        {
            detail = "No se encontraron métodos GetDeviceTime* en este SDK.";
        }
        else
        {
            var signatures = string.Join(
                ", ",
                methods
                    .Select(m => $"{m.Name}/{m.GetParameters().Length}")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8));
            detail = "Firmas detectadas: " + signatures + ".";
        }

        return false;
    }

    private static bool TryInvokeTimeMethod(object sdkObj, MethodInfo method, int machine, out DateTime parsed)
    {
        parsed = default;

        if (method.Name.Equals("GetDeviceTime2", StringComparison.OrdinalIgnoreCase)
            || method.Name.Equals("GetDeviceTime", StringComparison.OrdinalIgnoreCase)
            || method.Name.Equals("SSR_GetDeviceTime", StringComparison.OrdinalIgnoreCase))
        {
            if (TryInvokeDatePartsMethod(sdkObj, method, machine, out parsed))
            {
                return true;
            }
        }

        if (method.Name.Equals("GetDeviceTimeStr", StringComparison.OrdinalIgnoreCase)
            || method.Name.Equals("GetDeviceTime", StringComparison.OrdinalIgnoreCase))
        {
            if (TryInvokeDateStringMethod(sdkObj, method, machine, out parsed))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryInvokeDatePartsMethod(object sdkObj, MethodInfo method, int machine, out DateTime parsed)
    {
        parsed = default;
        var p = method.GetParameters();

        object?[] args;
        if (p.Length == 7 && p[0].ParameterType == typeof(int))
        {
            args = new object?[] { machine, 0, 0, 0, 0, 0, 0 };
        }
        else if (p.Length == 6)
        {
            args = new object?[] { 0, 0, 0, 0, 0, 0 };
        }
        else
        {
            return false;
        }

        var result = method.Invoke(sdkObj, args);
        if (!IsSuccessfulResult(result))
        {
            return false;
        }

        var offset = args.Length == 7 ? 1 : 0;
        if (!TryToInt(args[offset + 0], out var year)
            || !TryToInt(args[offset + 1], out var month)
            || !TryToInt(args[offset + 2], out var day)
            || !TryToInt(args[offset + 3], out var hour)
            || !TryToInt(args[offset + 4], out var minute)
            || !TryToInt(args[offset + 5], out var second))
        {
            return false;
        }

        try
        {
            parsed = new DateTime(year, month, day, hour, minute, second);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryInvokeDateStringMethod(object sdkObj, MethodInfo method, int machine, out DateTime parsed)
    {
        parsed = default;
        var p = method.GetParameters();

        object?[] args;
        if (p.Length == 2 && p[0].ParameterType == typeof(int))
        {
            args = new object?[] { machine, string.Empty };
        }
        else if (p.Length == 1)
        {
            args = new object?[] { string.Empty };
        }
        else
        {
            return false;
        }

        var result = method.Invoke(sdkObj, args);
        if (!IsSuccessfulResult(result))
        {
            return false;
        }

        var text = Convert.ToString(args[^1]);
        return DateTime.TryParse(text, out parsed);
    }

    private static bool IsSuccessfulResult(object? result)
    {
        if (result is null)
        {
            return true;
        }

        if (result is bool b)
        {
            return b;
        }

        if (result is int i)
        {
            return i != 0;
        }

        return true;
    }

    private static bool TryToInt(object? value, out int result)
    {
        if (value is int i)
        {
            result = i;
            return true;
        }

        if (value is short s)
        {
            result = s;
            return true;
        }

        if (value is long l && l is >= int.MinValue and <= int.MaxValue)
        {
            result = (int)l;
            return true;
        }

        if (value is string t && int.TryParse(t, out var parsed))
        {
            result = parsed;
            return true;
        }

        result = 0;
        return false;
    }
}
