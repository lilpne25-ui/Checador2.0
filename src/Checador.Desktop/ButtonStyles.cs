using System.Drawing;
using System.Windows.Forms;

namespace Checador.Desktop
{
    public static class ButtonStyles
    {
        public static void ApplyPrimary(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = Color.FromArgb(0, 120, 212);
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(16, 110, 190);
            btn.Cursor = Cursors.Hand;
            btn.Font = new Font("Segoe UI", 9f);
        }

        public static void ApplySecondary(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = Color.Transparent;
            btn.ForeColor = Color.FromArgb(0, 120, 212);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 212);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 252);
            btn.Cursor = Cursors.Hand;
            btn.Font = new Font("Segoe UI", 9f);
        }

        public static void ApplyDanger(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = Color.Transparent;
            btn.ForeColor = Color.FromArgb(216, 59, 1);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(216, 59, 1);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(253, 243, 240);
            btn.Cursor = Cursors.Hand;
            btn.Font = new Font("Segoe UI", 9f);
        }
    }
}
