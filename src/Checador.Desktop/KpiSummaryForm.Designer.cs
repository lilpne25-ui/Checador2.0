namespace Checador.Desktop;

partial class KpiSummaryForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        pnlHeader = new Panel();
        lblHeaderSubtitle = new Label();
        lblHeaderTitle = new Label();
        picHeaderIcon = new PictureBox();
        tlpMetrics = new TableLayoutPanel();
        pnlProcessed = new Panel();
        lblProcessedValue = new Label();
        lblProcessedTitle = new Label();
        pnlMissing = new Panel();
        lblMissingValue = new Label();
        lblMissingTitle = new Label();
        pnlWorked = new Panel();
        lblWorkedValue = new Label();
        lblWorkedTitle = new Label();
        pnlExtra = new Panel();
        lblExtraValue = new Label();
        lblExtraTitle = new Label();
        pnlSeparator = new Panel();
        tlpDetails = new TableLayoutPanel();
        lblReportsTitle = new Label();
        lblReportsValue = new Label();
        lblFolderTitle = new Label();
        lblFolderValue = new Label();
        flpFooter = new FlowLayoutPanel();
        btnClose = new Button();
        pnlHeader.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)picHeaderIcon).BeginInit();
        tlpMetrics.SuspendLayout();
        pnlProcessed.SuspendLayout();
        pnlMissing.SuspendLayout();
        pnlWorked.SuspendLayout();
        pnlExtra.SuspendLayout();
        tlpDetails.SuspendLayout();
        flpFooter.SuspendLayout();
        SuspendLayout();
        // 
        // pnlHeader
        // 
        pnlHeader.Controls.Add(lblHeaderSubtitle);
        pnlHeader.Controls.Add(lblHeaderTitle);
        pnlHeader.Controls.Add(picHeaderIcon);
        pnlHeader.Dock = DockStyle.Top;
        pnlHeader.Location = new Point(0, 0);
        pnlHeader.Name = "pnlHeader";
        pnlHeader.Padding = new Padding(16, 16, 16, 8);
        pnlHeader.Size = new Size(420, 90);
        pnlHeader.TabIndex = 0;
        // 
        // lblHeaderSubtitle
        // 
        lblHeaderSubtitle.AutoEllipsis = true;
        lblHeaderSubtitle.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblHeaderSubtitle.ForeColor = Color.FromArgb(102, 102, 102);
        lblHeaderSubtitle.Location = new Point(74, 46);
        lblHeaderSubtitle.Name = "lblHeaderSubtitle";
        lblHeaderSubtitle.Size = new Size(330, 34);
        lblHeaderSubtitle.TabIndex = 2;
        lblHeaderSubtitle.Tag = "secondary";
        lblHeaderSubtitle.Text = "Resumen de la corrida actual.";
        // 
        // lblHeaderTitle
        // 
        lblHeaderTitle.AutoSize = true;
        lblHeaderTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold, GraphicsUnit.Point);
        lblHeaderTitle.ForeColor = Color.FromArgb(30, 30, 30);
        lblHeaderTitle.Location = new Point(74, 18);
        lblHeaderTitle.Name = "lblHeaderTitle";
        lblHeaderTitle.Size = new Size(193, 25);
        lblHeaderTitle.TabIndex = 1;
        lblHeaderTitle.Text = "Generación completada";
        // 
        // picHeaderIcon
        // 
        picHeaderIcon.Location = new Point(20, 20);
        picHeaderIcon.Name = "picHeaderIcon";
        picHeaderIcon.Size = new Size(42, 42);
        picHeaderIcon.TabIndex = 0;
        picHeaderIcon.TabStop = false;
        // 
        // tlpMetrics
        // 
        tlpMetrics.ColumnCount = 2;
        tlpMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tlpMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tlpMetrics.Controls.Add(pnlProcessed, 0, 0);
        tlpMetrics.Controls.Add(pnlMissing, 1, 0);
        tlpMetrics.Controls.Add(pnlWorked, 0, 1);
        tlpMetrics.Controls.Add(pnlExtra, 1, 1);
        tlpMetrics.Dock = DockStyle.Top;
        tlpMetrics.Location = new Point(0, 90);
        tlpMetrics.Name = "tlpMetrics";
        tlpMetrics.Padding = new Padding(10, 6, 10, 6);
        tlpMetrics.RowCount = 2;
        tlpMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tlpMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tlpMetrics.Size = new Size(420, 182);
        tlpMetrics.TabIndex = 1;
        // 
        // pnlProcessed
        // 
        pnlProcessed.BackColor = Color.FromArgb(245, 245, 245);
        pnlProcessed.Controls.Add(lblProcessedValue);
        pnlProcessed.Controls.Add(lblProcessedTitle);
        pnlProcessed.Dock = DockStyle.Fill;
        pnlProcessed.Location = new Point(16, 12);
        pnlProcessed.Margin = new Padding(6);
        pnlProcessed.Name = "pnlProcessed";
        pnlProcessed.Padding = new Padding(12);
        pnlProcessed.Size = new Size(188, 79);
        pnlProcessed.TabIndex = 0;
        // 
        // lblProcessedValue
        // 
        lblProcessedValue.AutoSize = true;
        lblProcessedValue.Font = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point);
        lblProcessedValue.ForeColor = Color.FromArgb(0, 120, 212);
        lblProcessedValue.Location = new Point(11, 30);
        lblProcessedValue.Name = "lblProcessedValue";
        lblProcessedValue.Size = new Size(47, 37);
        lblProcessedValue.TabIndex = 1;
        lblProcessedValue.Tag = "semantic";
        lblProcessedValue.Text = "0";
        // 
        // lblProcessedTitle
        // 
        lblProcessedTitle.AutoSize = true;
        lblProcessedTitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblProcessedTitle.ForeColor = Color.FromArgb(102, 102, 102);
        lblProcessedTitle.Location = new Point(12, 12);
        lblProcessedTitle.Name = "lblProcessedTitle";
        lblProcessedTitle.Size = new Size(126, 15);
        lblProcessedTitle.TabIndex = 0;
        lblProcessedTitle.Tag = "secondary";
        lblProcessedTitle.Text = "Empleados procesados";
        // 
        // pnlMissing
        // 
        pnlMissing.BackColor = Color.FromArgb(245, 245, 245);
        pnlMissing.Controls.Add(lblMissingValue);
        pnlMissing.Controls.Add(lblMissingTitle);
        pnlMissing.Dock = DockStyle.Fill;
        pnlMissing.Location = new Point(216, 12);
        pnlMissing.Margin = new Padding(6);
        pnlMissing.Name = "pnlMissing";
        pnlMissing.Padding = new Padding(12);
        pnlMissing.Size = new Size(188, 79);
        pnlMissing.TabIndex = 1;
        // 
        // lblMissingValue
        // 
        lblMissingValue.AutoSize = true;
        lblMissingValue.Font = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point);
        lblMissingValue.ForeColor = Color.FromArgb(202, 80, 16);
        lblMissingValue.Location = new Point(11, 30);
        lblMissingValue.Name = "lblMissingValue";
        lblMissingValue.Size = new Size(47, 37);
        lblMissingValue.TabIndex = 1;
        lblMissingValue.Tag = "semantic";
        lblMissingValue.Text = "0";
        // 
        // lblMissingTitle
        // 
        lblMissingTitle.AutoSize = true;
        lblMissingTitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblMissingTitle.ForeColor = Color.FromArgb(102, 102, 102);
        lblMissingTitle.Location = new Point(12, 12);
        lblMissingTitle.Name = "lblMissingTitle";
        lblMissingTitle.Size = new Size(66, 15);
        lblMissingTitle.TabIndex = 0;
        lblMissingTitle.Tag = "secondary";
        lblMissingTitle.Text = "Sin registro";
        // 
        // pnlWorked
        // 
        pnlWorked.BackColor = Color.FromArgb(245, 245, 245);
        pnlWorked.Controls.Add(lblWorkedValue);
        pnlWorked.Controls.Add(lblWorkedTitle);
        pnlWorked.Dock = DockStyle.Fill;
        pnlWorked.Location = new Point(16, 103);
        pnlWorked.Margin = new Padding(6);
        pnlWorked.Name = "pnlWorked";
        pnlWorked.Padding = new Padding(12);
        pnlWorked.Size = new Size(188, 73);
        pnlWorked.TabIndex = 2;
        // 
        // lblWorkedValue
        // 
        lblWorkedValue.AutoSize = true;
        lblWorkedValue.Font = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point);
        lblWorkedValue.ForeColor = Color.FromArgb(16, 124, 16);
        lblWorkedValue.Location = new Point(11, 29);
        lblWorkedValue.Name = "lblWorkedValue";
        lblWorkedValue.Size = new Size(75, 37);
        lblWorkedValue.TabIndex = 1;
        lblWorkedValue.Tag = "semantic";
        lblWorkedValue.Text = "0.00";
        // 
        // lblWorkedTitle
        // 
        lblWorkedTitle.AutoSize = true;
        lblWorkedTitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblWorkedTitle.ForeColor = Color.FromArgb(102, 102, 102);
        lblWorkedTitle.Location = new Point(12, 12);
        lblWorkedTitle.Name = "lblWorkedTitle";
        lblWorkedTitle.Size = new Size(89, 15);
        lblWorkedTitle.TabIndex = 0;
        lblWorkedTitle.Tag = "secondary";
        lblWorkedTitle.Text = "Horas trabajadas";
        // 
        // pnlExtra
        // 
        pnlExtra.BackColor = Color.FromArgb(245, 245, 245);
        pnlExtra.Controls.Add(lblExtraValue);
        pnlExtra.Controls.Add(lblExtraTitle);
        pnlExtra.Dock = DockStyle.Fill;
        pnlExtra.Location = new Point(216, 103);
        pnlExtra.Margin = new Padding(6);
        pnlExtra.Name = "pnlExtra";
        pnlExtra.Padding = new Padding(12);
        pnlExtra.Size = new Size(188, 73);
        pnlExtra.TabIndex = 3;
        // 
        // lblExtraValue
        // 
        lblExtraValue.AutoSize = true;
        lblExtraValue.Font = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point);
        lblExtraValue.ForeColor = Color.FromArgb(92, 45, 145);
        lblExtraValue.Location = new Point(11, 29);
        lblExtraValue.Name = "lblExtraValue";
        lblExtraValue.Size = new Size(75, 37);
        lblExtraValue.TabIndex = 1;
        lblExtraValue.Tag = "semantic";
        lblExtraValue.Text = "0.00";
        // 
        // lblExtraTitle
        // 
        lblExtraTitle.AutoSize = true;
        lblExtraTitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblExtraTitle.ForeColor = Color.FromArgb(102, 102, 102);
        lblExtraTitle.Location = new Point(12, 12);
        lblExtraTitle.Name = "lblExtraTitle";
        lblExtraTitle.Size = new Size(66, 15);
        lblExtraTitle.TabIndex = 0;
        lblExtraTitle.Tag = "secondary";
        lblExtraTitle.Text = "Horas extra";
        // 
        // pnlSeparator
        // 
        pnlSeparator.BackColor = Color.FromArgb(224, 224, 224);
        pnlSeparator.Dock = DockStyle.Top;
        pnlSeparator.Location = new Point(0, 272);
        pnlSeparator.Name = "pnlSeparator";
        pnlSeparator.Size = new Size(420, 1);
        pnlSeparator.TabIndex = 2;
        // 
        // tlpDetails
        // 
        tlpDetails.ColumnCount = 2;
        tlpDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165F));
        tlpDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlpDetails.Controls.Add(lblReportsTitle, 0, 0);
        tlpDetails.Controls.Add(lblReportsValue, 1, 0);
        tlpDetails.Controls.Add(lblFolderTitle, 0, 1);
        tlpDetails.Controls.Add(lblFolderValue, 1, 1);
        tlpDetails.Dock = DockStyle.Top;
        tlpDetails.Location = new Point(0, 273);
        tlpDetails.Name = "tlpDetails";
        tlpDetails.Padding = new Padding(16, 10, 16, 0);
        tlpDetails.RowCount = 2;
        tlpDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
        tlpDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        tlpDetails.Size = new Size(420, 72);
        tlpDetails.TabIndex = 3;
        // 
        // lblReportsTitle
        // 
        lblReportsTitle.AutoSize = true;
        lblReportsTitle.Dock = DockStyle.Fill;
        lblReportsTitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblReportsTitle.ForeColor = Color.FromArgb(102, 102, 102);
        lblReportsTitle.Location = new Point(19, 10);
        lblReportsTitle.Name = "lblReportsTitle";
        lblReportsTitle.Size = new Size(159, 27);
        lblReportsTitle.TabIndex = 0;
        lblReportsTitle.Tag = "secondary";
        lblReportsTitle.Text = "Reportes generados:";
        lblReportsTitle.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblReportsValue
        // 
        lblReportsValue.AutoEllipsis = true;
        lblReportsValue.Dock = DockStyle.Fill;
        lblReportsValue.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblReportsValue.ForeColor = Color.FromArgb(32, 32, 32);
        lblReportsValue.Location = new Point(184, 10);
        lblReportsValue.Name = "lblReportsValue";
        lblReportsValue.Size = new Size(217, 27);
        lblReportsValue.TabIndex = 1;
        lblReportsValue.Text = "0";
        lblReportsValue.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblFolderTitle
        // 
        lblFolderTitle.AutoSize = true;
        lblFolderTitle.Dock = DockStyle.Fill;
        lblFolderTitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblFolderTitle.ForeColor = Color.FromArgb(102, 102, 102);
        lblFolderTitle.Location = new Point(19, 37);
        lblFolderTitle.Name = "lblFolderTitle";
        lblFolderTitle.Size = new Size(159, 35);
        lblFolderTitle.TabIndex = 2;
        lblFolderTitle.Tag = "secondary";
        lblFolderTitle.Text = "Carpeta de salida:";
        lblFolderTitle.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblFolderValue
        // 
        lblFolderValue.AutoEllipsis = true;
        lblFolderValue.Dock = DockStyle.Fill;
        lblFolderValue.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblFolderValue.ForeColor = Color.FromArgb(32, 32, 32);
        lblFolderValue.Location = new Point(184, 37);
        lblFolderValue.Name = "lblFolderValue";
        lblFolderValue.Size = new Size(217, 35);
        lblFolderValue.TabIndex = 3;
        lblFolderValue.Text = "N/D";
        lblFolderValue.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // flpFooter
        // 
        flpFooter.Controls.Add(btnClose);
        flpFooter.Dock = DockStyle.Bottom;
        flpFooter.FlowDirection = FlowDirection.RightToLeft;
        flpFooter.Location = new Point(0, 345);
        flpFooter.Name = "flpFooter";
        flpFooter.Padding = new Padding(12, 8, 12, 8);
        flpFooter.Size = new Size(420, 39);
        flpFooter.TabIndex = 4;
        // 
        // btnClose
        // 
        btnClose.DialogResult = DialogResult.OK;
        btnClose.Location = new Point(293, 8);
        btnClose.Margin = new Padding(0);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(103, 30);
        btnClose.TabIndex = 0;
        btnClose.Text = "Cerrar";
        btnClose.UseVisualStyleBackColor = true;
        // 
        // KpiSummaryForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(420, 384);
        Controls.Add(flpFooter);
        Controls.Add(tlpDetails);
        Controls.Add(pnlSeparator);
        Controls.Add(tlpMetrics);
        Controls.Add(pnlHeader);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "KpiSummaryForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Resumen KPI de corrida";
        pnlHeader.ResumeLayout(false);
        pnlHeader.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)picHeaderIcon).EndInit();
        tlpMetrics.ResumeLayout(false);
        pnlProcessed.ResumeLayout(false);
        pnlProcessed.PerformLayout();
        pnlMissing.ResumeLayout(false);
        pnlMissing.PerformLayout();
        pnlWorked.ResumeLayout(false);
        pnlWorked.PerformLayout();
        pnlExtra.ResumeLayout(false);
        pnlExtra.PerformLayout();
        tlpDetails.ResumeLayout(false);
        tlpDetails.PerformLayout();
        flpFooter.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private Panel pnlHeader;
    private PictureBox picHeaderIcon;
    private Label lblHeaderTitle;
    private Label lblHeaderSubtitle;
    private TableLayoutPanel tlpMetrics;
    private Panel pnlProcessed;
    private Label lblProcessedTitle;
    private Label lblProcessedValue;
    private Panel pnlMissing;
    private Label lblMissingTitle;
    private Label lblMissingValue;
    private Panel pnlWorked;
    private Label lblWorkedTitle;
    private Label lblWorkedValue;
    private Panel pnlExtra;
    private Label lblExtraTitle;
    private Label lblExtraValue;
    private Panel pnlSeparator;
    private TableLayoutPanel tlpDetails;
    private Label lblReportsTitle;
    private Label lblReportsValue;
    private Label lblFolderTitle;
    private Label lblFolderValue;
    private FlowLayoutPanel flpFooter;
    private Button btnClose;
}
