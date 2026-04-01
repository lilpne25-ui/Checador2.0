namespace Checador.Desktop;

public static class AnimationHelper
{
    public static async Task AnimateProgressAsync(
        ProgressBar bar,
        int targetValue,
        int durationMs = 400)
    {
        if (bar.Style == ProgressBarStyle.Marquee)
        {
            return;
        }

        int start = bar.Value;
        int steps = 20;
        int delay = durationMs / steps;

        for (int i = 1; i <= steps; i++)
        {
            int next = start + (int)((targetValue - start) * (i / (float)steps));
            if (bar.IsHandleCreated)
            {
                bar.Invoke(() => bar.Value = Math.Clamp(next, bar.Minimum, bar.Maximum));
            }
            await Task.Delay(delay);
        }
    }

    public static async Task AnimateLabelColorAsync(
        Label label,
        Color from,
        Color to,
        int durationMs = 300)
    {
        int steps = 15;
        int delay = durationMs / steps;

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            var blended = Color.FromArgb(
                (int)(from.A + (to.A - from.A) * t),
                (int)(from.R + (to.R - from.R) * t),
                (int)(from.G + (to.G - from.G) * t),
                (int)(from.B + (to.B - from.B) * t));
            if (label.IsHandleCreated)
            {
                label.Invoke(() => label.ForeColor = blended);
            }
            await Task.Delay(delay);
        }
    }

    public static async Task AnimatePanelColorAsync(
        Panel panel,
        Color from,
        Color to,
        int durationMs = 500)
    {
        int steps = 20;
        int delay = durationMs / steps;

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            var blended = Color.FromArgb(
                (int)(from.R + (to.R - from.R) * t),
                (int)(from.G + (to.G - from.G) * t),
                (int)(from.B + (to.B - from.B) * t));
            if (panel.IsHandleCreated)
            {
                panel.Invoke(() => panel.BackColor = blended);
            }
            await Task.Delay(delay);
        }
    }
}
