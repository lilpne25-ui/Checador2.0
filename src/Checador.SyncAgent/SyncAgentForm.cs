using Checador.Infrastructure.Access;

namespace Checador.SyncAgent;

public sealed class SyncAgentForm : Form
{
    private readonly SqlDeviceSyncService _syncService;
    private readonly NumericUpDown _numMinutes;
    private readonly Button _btnToggle;
    private readonly Button _btnRunNow;
    private readonly Label _lblState;
    private readonly TextBox _txtLog;
    private readonly System.Windows.Forms.Timer _timer;

    private bool _running;
    private bool _busy;

    public SyncAgentForm(SqlDeviceSyncService syncService)
    {
        _syncService = syncService;

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
            Text = "Intervalo (minutos):"
        };

        _numMinutes = new NumericUpDown
        {
            Left = 200,
            Top = 54,
            Width = 80,
            Minimum = 1,
            Maximum = 240,
            Value = 15
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
        _timer.Tick += async (_, _) => await RunCycleAsync("Ejecución programada");

        Shown += async (_, _) =>
        {
            try
            {
                await _syncService.EnsureReadyAsync();
                Log("SyncAgent listo. Puedes activar AutoSync.");
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
        await RunCycleAsync("Ejecución inicial");
    }

    private void Start()
    {
        var minutes = (int)_numMinutes.Value;
        _timer.Interval = Math.Max(1, minutes) * 60 * 1000;
        _timer.Start();
        _running = true;
        _numMinutes.Enabled = false;
        _btnToggle.Text = "⏹ Detener AutoSync";
        _btnToggle.BackColor = Color.FromArgb(181, 53, 53);
        _lblState.Text = $"Estado: activo (cada {minutes} min)";
        Log($"AutoSync ACTIVADO cada {minutes} minutos.");
    }

    private void Stop()
    {
        _timer.Stop();
        _running = false;
        _numMinutes.Enabled = true;
        _btnToggle.Text = "▶ Activar AutoSync";
        _btnToggle.BackColor = Color.FromArgb(0, 120, 215);
        _lblState.Text = "Estado: detenido";
        Log("AutoSync DETENIDO.");
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
}
