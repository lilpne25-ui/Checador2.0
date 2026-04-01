using Checador.Infrastructure.Access;

namespace Checador.Desktop;

public sealed class AutoSyncControlForm : Form
{
    private readonly SqlDeviceSyncService _syncService;
    private readonly NumericUpDown _numMinutes;
    private readonly Button _btnToggle;
    private readonly Label _lblStatus;
    private readonly TextBox _txtLog;
    private readonly System.Windows.Forms.Timer _timer;

    private bool _running;
    private bool _busy;

    public AutoSyncControlForm(SqlDeviceSyncService syncService, int initialMinutes = 15)
    {
        _syncService = syncService;
        ThemeManager.ThemeChanged += (s, e) =>
        {
            if (IsHandleCreated)
            {
                Invoke(() => ThemeManager.ApplyToForm(this));
            }
        };
        Load += (_, _) => ThemeManager.ApplyToForm(this);

        Text = "AutoSync Servidor (GUI)";
        StartPosition = FormStartPosition.CenterParent;
        Width = 640;
        Height = 420;
        MinimumSize = new Size(560, 360);
        BackColor = Color.FromArgb(245, 248, 252);

        var lblTitle = new Label
        {
            Left = 16,
            Top = 14,
            Width = 580,
            Height = 28,
            Text = "Sincronizador automático SQL ↔ Reloj",
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 39, 58)
        };

        var lblInterval = new Label
        {
            Left = 16,
            Top = 58,
            Width = 190,
            Height = 24,
            Text = "Ejecutar cada (minutos):"
        };

        _numMinutes = new NumericUpDown
        {
            Left = 210,
            Top = 55,
            Width = 80,
            Minimum = 1,
            Maximum = 240,
            Value = Math.Clamp(initialMinutes, 1, 240)
        };

        _btnToggle = new Button
        {
            Left = 310,
            Top = 52,
            Width = 150,
            Height = 32,
            Text = "▶ Activar AutoSync",
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnToggle.FlatAppearance.BorderSize = 0;
        _btnToggle.Click += btnToggle_Click;

        _lblStatus = new Label
        {
            Left = 16,
            Top = 95,
            Width = 590,
            Height = 24,
            Text = "Estado: detenido",
            ForeColor = Color.FromArgb(90, 100, 115),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        _txtLog = new TextBox
        {
            Left = 16,
            Top = 126,
            Width = 590,
            Height = 236,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F),
            BackColor = Color.White
        };

        Controls.Add(lblTitle);
        Controls.Add(lblInterval);
        Controls.Add(_numMinutes);
        Controls.Add(_btnToggle);
        Controls.Add(_lblStatus);
        Controls.Add(_txtLog);

        _timer = new System.Windows.Forms.Timer();
        _timer.Tick += timer_Tick;

        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _running = false;
        };
    }

    private async void btnToggle_Click(object? sender, EventArgs e)
    {
        if (_running)
        {
            StopAutoSync();
            return;
        }

        StartAutoSync();
        await RunSyncCycleAsync("Ejecución inicial manual");
    }

    private void StartAutoSync()
    {
        var minutes = (int)_numMinutes.Value;
        _timer.Interval = Math.Max(1, minutes) * 60 * 1000;
        _timer.Start();
        _running = true;
        _numMinutes.Enabled = false;
        _btnToggle.Text = "⏹ Detener AutoSync";
        _btnToggle.BackColor = Color.FromArgb(181, 53, 53);
        _lblStatus.Text = $"Estado: activo (cada {minutes} min)";
        Log($"AutoSync activado cada {minutes} minutos.");
    }

    private void StopAutoSync()
    {
        _timer.Stop();
        _running = false;
        _numMinutes.Enabled = true;
        _btnToggle.Text = "▶ Activar AutoSync";
        _btnToggle.BackColor = Color.FromArgb(0, 120, 215);
        _lblStatus.Text = "Estado: detenido";
        Log("AutoSync detenido.");
    }

    private async void timer_Tick(object? sender, EventArgs e)
    {
        await RunSyncCycleAsync("Ejecución programada");
    }

    private async Task RunSyncCycleAsync(string reason)
    {
        if (_busy)
        {
            Log("Ciclo omitido: aún hay una sincronización en curso.");
            return;
        }

        _busy = true;
        try
        {
            Log($"{reason}: iniciando descarga de registros...");
            var result = await _syncService.DownloadAttendanceLogsAsync();
            Log($"OK · Leídas={result.DevicePunches}, Insertadas={result.InsertedPunches}, Duplicadas={result.DuplicatePunches}");
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
