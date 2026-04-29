using Checador.Infrastructure.Access;
using System.Globalization;

namespace Checador.SyncAgent;

public sealed class SyncAgentForm : Form
{
    private readonly SqlDeviceSyncService _syncService;
    private readonly SyncAgentConfiguration _configuration;
    private readonly NumericUpDown _numMinutes;
    private readonly Button _btnToggle;
    private readonly Button _btnRunNow;
    private readonly Label _lblState;
    private readonly TextBox _txtLog;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly List<ScheduleWindow> _scheduleWindows = [];

    private bool _running;
    private bool _busy;
    private DateTime? _lastScheduledSlot;
    private bool? _insideWindow;
    private string? _initialScheduleWarning;

    public SyncAgentForm(SqlDeviceSyncService syncService, SyncAgentConfiguration configuration)
    {
        _syncService = syncService;
        _configuration = configuration;
        InitializeSchedule();

        Text = "Checador SyncAgent (Servidor)";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 760;
        Height = 500;
        MinimumSize = new Size(680, 420);
        BackColor = Color.FromArgb(245, 248, 252);

        var title = new Label
        {
            Left = 16,
            Top = 14,
            Width = 700,
            Height = 30,
            Text = "Sincronizador externo del servidor (independiente de Checador)",
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 39, 58)
        };

        var lblInterval = new Label
        {
            Left = 16,
            Top = 56,
            Width = 180,
            Text = IsWindowScheduleEnabled()
                ? "Intervalo ventana (min):"
                : "Intervalo (minutos):"
        };

        var initialMinutes = IsWindowScheduleEnabled()
            ? ClampInterval(GetScheduleIntervalMinutes())
            : Math.Max(1, _configuration.Sql.AutoSyncMinutes);

        _numMinutes = new NumericUpDown
        {
            Left = 200,
            Top = 54,
            Width = 80,
            Minimum = 1,
            Maximum = 240,
            Value = initialMinutes,
            Enabled = !IsWindowScheduleEnabled()
        };

        _btnToggle = new Button
        {
            Left = 300,
            Top = 50,
            Width = 170,
            Height = 34,
            Text = "▶ Activar AutoSync",
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnToggle.FlatAppearance.BorderSize = 0;
        _btnToggle.Click += btnToggle_Click;

        _btnRunNow = new Button
        {
            Left = 480,
            Top = 50,
            Width = 140,
            Height = 34,
            Text = "⟳ Sync ahora",
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnRunNow.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 230);
        _btnRunNow.Click += async (_, _) => await RunCycleAsync("Ejecución manual");

        _lblState = new Label
        {
            Left = 16,
            Top = 98,
            Width = 700,
            Height = 24,
            Text = "Estado: detenido",
            ForeColor = Color.FromArgb(90, 100, 115),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        _txtLog = new TextBox
        {
            Left = 16,
            Top = 128,
            Width = 710,
            Height = 320,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F),
            BackColor = Color.White
        };

        Controls.Add(title);
        Controls.Add(lblInterval);
        Controls.Add(_numMinutes);
        Controls.Add(_btnToggle);
        Controls.Add(_btnRunNow);
        Controls.Add(_lblState);
        Controls.Add(_txtLog);

        _timer = new System.Windows.Forms.Timer();
        _timer.Tick += async (_, _) => await OnTimerTickAsync();

        Shown += async (_, _) =>
        {
            try
            {
                await _syncService.EnsureReadyAsync();
                Log("SyncAgent listo. Puedes activar AutoSync.");
                if (IsWindowScheduleEnabled())
                {
                    Log($"Horario activo: {BuildScheduleSummary()} (cada {GetScheduleIntervalMinutes()} min).");
                    if (!string.IsNullOrWhiteSpace(_initialScheduleWarning))
                    {
                        Log(_initialScheduleWarning);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"ERROR inicializando: {ex.Message}");
            }
        };

        FormClosed += (_, _) => _timer.Stop();
    }

    private async void btnToggle_Click(object? sender, EventArgs e)
    {
        if (_running)
        {
            Stop();
            return;
        }

        Start();
        if (IsWindowScheduleEnabled())
        {
            await RunScheduledTickAsync("Ejecución inicial con horario");
        }
        else
        {
            await RunCycleAsync("Ejecución inicial");
        }
    }

    private void Start()
    {
        var minutes = IsWindowScheduleEnabled()
            ? GetScheduleIntervalMinutes()
            : (int)_numMinutes.Value;

        _timer.Interval = IsWindowScheduleEnabled()
            ? GetPollMilliseconds()
            : Math.Max(1, minutes) * 60 * 1000;

        _timer.Start();
        _running = true;
        _numMinutes.Enabled = false;
        _btnToggle.Text = "⏹ Detener AutoSync";
        _btnToggle.BackColor = Color.FromArgb(181, 53, 53);

        if (IsWindowScheduleEnabled())
        {
            _lblState.Text = $"Estado: activo por ventanas (cada {minutes} min)";
            Log($"AutoSync ACTIVADO por ventanas: {BuildScheduleSummary()} · cada {minutes} minutos.");
        }
        else
        {
            _lblState.Text = $"Estado: activo (cada {minutes} min)";
            Log($"AutoSync ACTIVADO cada {minutes} minutos.");
        }
    }

    private void Stop()
    {
        _timer.Stop();
        _running = false;
        _numMinutes.Enabled = !IsWindowScheduleEnabled();
        _btnToggle.Text = "▶ Activar AutoSync";
        _btnToggle.BackColor = Color.FromArgb(0, 120, 215);
        _lblState.Text = "Estado: detenido";
        _lastScheduledSlot = null;
        _insideWindow = null;
        Log("AutoSync DETENIDO.");
    }

    private async Task OnTimerTickAsync()
    {
        if (IsWindowScheduleEnabled())
        {
            await RunScheduledTickAsync("Ejecución programada");
            return;
        }

        await RunCycleAsync("Ejecución programada");
    }

    private async Task RunScheduledTickAsync(string reason)
    {
        if (!TryGetCurrentWindow(DateTime.Now, out var window, out var slot))
        {
            if (_insideWindow != false)
            {
                _insideWindow = false;
                Log("Fuera de ventana horaria. Esperando próximo bloque programado.");
            }

            return;
        }

        if (_insideWindow != true)
        {
            _insideWindow = true;
            Log($"Dentro de ventana horaria {window!.Display}.");
        }

        if (_lastScheduledSlot.HasValue && _lastScheduledSlot.Value == slot)
        {
            return;
        }

        _lastScheduledSlot = slot;
        await RunCycleAsync($"{reason} ({window!.Display} @ {slot:HH:mm})");
    }

    private async Task RunCycleAsync(string reason)
    {
        if (_busy)
        {
            Log("Ciclo omitido: ya hay sync en proceso.");
            return;
        }

        _busy = true;
        try
        {
            Log($"{reason}: descargando marcas...");
            var result = await _syncService.DownloadAttendanceLogsAsync();
            Log($"OK · Leídas={result.DevicePunches}, Insertadas={result.InsertedPunches}, Duplicadas={result.DuplicatePunches}");
            Log(result.Message);
        }
        catch (Exception ex)
        {
            Log($"ERROR · {ex.Message}");
        }
        finally
        {
            _busy = false;
        }
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (string.IsNullOrWhiteSpace(_txtLog.Text))
        {
            _txtLog.Text = line;
        }
        else
        {
            _txtLog.AppendText(Environment.NewLine + line);
        }

        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.ScrollToCaret();
    }

    private void InitializeSchedule()
    {
        _scheduleWindows.Clear();
        if (!IsWindowScheduleEnabled())
        {
            return;
        }

        var sourceWindows = _configuration.AutoSchedule.Windows ?? [];
        foreach (var source in sourceWindows)
        {
            if (!TryParseClock(source.Start, out var start) || !TryParseClock(source.End, out var end))
            {
                continue;
            }

            _scheduleWindows.Add(new ScheduleWindow(start, end));
        }

        if (_scheduleWindows.Count == 0)
        {
            _scheduleWindows.Add(new ScheduleWindow(TimeSpan.Zero, new TimeSpan(23, 59, 0)));
            _initialScheduleWarning = "No se pudieron leer ventanas horarias de configuración; se usará ventana completa 00:00-23:59.";
        }
    }

    private bool IsWindowScheduleEnabled()
        => _configuration.AutoSchedule.Enabled;

    private int GetScheduleIntervalMinutes()
        => Math.Max(1, _configuration.AutoSchedule.IntervalMinutes);

    private int GetPollMilliseconds()
        => Math.Max(5, _configuration.AutoSchedule.PollSeconds) * 1000;

    private static decimal ClampInterval(int interval)
        => Math.Min(240, Math.Max(1, interval));

    private string BuildScheduleSummary()
    {
        return string.Join(" | ", _scheduleWindows.Select(x => x.Display));
    }

    private bool TryGetCurrentWindow(DateTime now, out ScheduleWindow? window, out DateTime slot)
    {
        foreach (var candidate in _scheduleWindows)
        {
            if (!candidate.Contains(now.TimeOfDay))
            {
                continue;
            }

            var interval = GetScheduleIntervalMinutes();
            var alignedMinute = (now.Minute / interval) * interval;
            slot = new DateTime(now.Year, now.Month, now.Day, now.Hour, alignedMinute, 0);
            window = candidate;
            return true;
        }

        slot = default;
        window = null;
        return false;
    }

    private static bool TryParseClock(string value, out TimeSpan parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var raw = value.Trim();
        var formats = new[]
        {
            "H:mm",
            "HH:mm",
            "H:mm:ss",
            "HH:mm:ss",
            "h:mmtt",
            "hh:mmtt",
            "h:mm tt",
            "hh:mm tt"
        };

        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtInvariant))
        {
            parsed = dtInvariant.TimeOfDay;
            return true;
        }

        if (DateTime.TryParse(raw, new CultureInfo("es-MX"), DateTimeStyles.None, out var dtEs))
        {
            parsed = dtEs.TimeOfDay;
            return true;
        }

        return false;
    }

    private sealed class ScheduleWindow
    {
        public ScheduleWindow(TimeSpan start, TimeSpan end)
        {
            Start = start;
            End = end;
            CrossesMidnight = end < start;
            Display = $"{start:hh\\:mm}-{end:hh\\:mm}";
        }

        public TimeSpan Start { get; }
        public TimeSpan End { get; }
        public bool CrossesMidnight { get; }
        public string Display { get; }

        public bool Contains(TimeSpan value)
        {
            if (!CrossesMidnight)
            {
                return value >= Start && value <= End;
            }

            return value >= Start || value <= End;
        }
    }
}
