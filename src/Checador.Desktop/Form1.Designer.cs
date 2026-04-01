namespace Checador.Desktop;

partial class Form1
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
        this.components = new System.ComponentModel.Container();
        this.pnlTopBar = new Panel();
        this.lblAppGlyph = new Label();
        this.lblTitle = new Label();
        this.lblSubtitle = new Label();
        this.grpFiltros = new RoundedGroupBox();
        this.lblUserId = new Label();
        this.cmbEmployee = new ComboBox();
        this.chkAllEmployees = new CheckBox();
        this.lblStart = new Label();
        this.dtpStart = new DateTimePicker();
        this.lblEnd = new Label();
        this.dtpEnd = new DateTimePicker();
        this.lblTheme = new Label();
        this.cmbTheme = new ComboBox();
        this.grpUbicaciones = new RoundedGroupBox();
        this.lblOutput = new Label();
        this.txtOutputDirectory = new TextBox();
        this.btnBrowseOutput = new Button();
        this.lblTemplate = new Label();
        this.txtTemplatePath = new TextBox();
        this.btnBrowseTemplate = new Button();
        this.grpAcciones = new RoundedGroupBox();
        this.flpActionButtons = new FlowLayoutPanel();
        this.btnGenerate = new Button();
        this.btnPreview = new Button();
        this.btnOpenReport = new Button();
        this.btnTestConnection = new Button();
        this.btnAdminSql = new Button();
        this.lblConnectionTitle = new Label();
        this.pnlConnectionIndicator = new Panel();
        this.lblConnectionValue = new StatusPillLabel();
        this.progressAction = new ProgressBar();
        this.grpEstado = new RoundedGroupBox();
        this.lblStatusCaption = new Label();
        this.txtStatus = new RichTextBox();
        this.tipMain = new AppToolTip(this.components);
        this.connectionTimer = new System.Windows.Forms.Timer(this.components);
        this.pnlTopBar.SuspendLayout();
        this.grpFiltros.SuspendLayout();
        this.grpUbicaciones.SuspendLayout();
        this.grpAcciones.SuspendLayout();
        this.grpEstado.SuspendLayout();
        this.SuspendLayout();

        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.ClientSize = new System.Drawing.Size(980, 550);
        this.MinimumSize = new Size(960, 540);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "Checador 2.0 - Reporte de Asistencia";

        this.pnlTopBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.pnlTopBar.BackColor = Color.FromArgb(255, 255, 255);
        this.pnlTopBar.Location = new Point(0, 0);
        this.pnlTopBar.Name = "pnlTopBar";
        this.pnlTopBar.Size = new Size(980, 74);

        this.lblAppGlyph.AutoSize = true;
        this.lblAppGlyph.Font = new Font("Segoe MDL2 Assets", 24F, FontStyle.Regular, GraphicsUnit.Point);
        this.lblAppGlyph.ForeColor = Color.FromArgb(0, 120, 215);
        this.lblAppGlyph.Location = new Point(18, 18);
        this.lblAppGlyph.Name = "lblAppGlyph";
        this.lblAppGlyph.Size = new Size(35, 32);
        this.lblAppGlyph.Text = "\uE823";

        this.lblTitle.AutoSize = true;
        this.lblTitle.Font = new Font("Segoe UI", 15F, FontStyle.Bold, GraphicsUnit.Point);
        this.lblTitle.ForeColor = Color.FromArgb(17, 37, 62);
        this.lblTitle.Location = new Point(60, 12);
        this.lblTitle.Name = "lblTitle";
        this.lblTitle.Size = new Size(396, 28);
        this.lblTitle.Text = "Checador 2.0 · Generador de Asistencias";

        this.lblSubtitle.AutoSize = true;
        this.lblSubtitle.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        this.lblSubtitle.ForeColor = Color.FromArgb(90, 100, 115);
        this.lblSubtitle.Location = new Point(62, 43);
        this.lblSubtitle.Name = "lblSubtitle";
        this.lblSubtitle.Size = new Size(449, 17);
        this.lblSubtitle.Tag = "secondary";
        this.lblSubtitle.Text = "Configura filtros y rutas para generar reportes individuales o masivos (todos).";

        this.pnlTopBar.Controls.Add(this.lblAppGlyph);
        this.pnlTopBar.Controls.Add(this.lblTitle);
        this.pnlTopBar.Controls.Add(this.lblSubtitle);

        this.grpFiltros.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.grpFiltros.BackColor = Color.White;
        this.grpFiltros.Controls.Add(this.lblUserId);
        this.grpFiltros.Controls.Add(this.cmbEmployee);
        this.grpFiltros.Controls.Add(this.chkAllEmployees);
        this.grpFiltros.Controls.Add(this.lblStart);
        this.grpFiltros.Controls.Add(this.dtpStart);
        this.grpFiltros.Controls.Add(this.lblEnd);
        this.grpFiltros.Controls.Add(this.dtpEnd);
        this.grpFiltros.Controls.Add(this.lblTheme);
        this.grpFiltros.Controls.Add(this.cmbTheme);
        this.grpFiltros.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
        this.grpFiltros.ForeColor = Color.FromArgb(17, 37, 62);
        this.grpFiltros.Location = new Point(20, 86);
        this.grpFiltros.Name = "grpFiltros";
        this.grpFiltros.Size = new Size(940, 115);
        this.grpFiltros.TabStop = false;
        this.grpFiltros.Text = "Filtros";

        this.lblUserId.AutoSize = true;
        this.lblUserId.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.lblUserId.Location = new Point(16, 33);
        this.lblUserId.Name = "lblUserId";
        this.lblUserId.Size = new Size(61, 15);
        this.lblUserId.Tag = "secondary";
        this.lblUserId.Text = "Empleado:";

        this.cmbEmployee.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cmbEmployee.FormattingEnabled = true;
        this.cmbEmployee.Location = new Point(130, 30);
        this.cmbEmployee.Name = "cmbEmployee";
        this.cmbEmployee.Size = new Size(220, 23);

        this.chkAllEmployees.AutoSize = true;
        this.chkAllEmployees.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.chkAllEmployees.Location = new Point(130, 68);
        this.chkAllEmployees.Name = "chkAllEmployees";
        this.chkAllEmployees.Size = new Size(174, 19);
        this.chkAllEmployees.Text = "Generar todos los empleados";
        this.chkAllEmployees.CheckedChanged += new EventHandler(this.chkAllEmployees_CheckedChanged);
        this.tipMain.SetToolTip(this.chkAllEmployees, "Si se activa, se genera un Excel por cada empleado del catálogo.");

        this.lblStart.AutoSize = true;
        this.lblStart.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.lblStart.Location = new Point(385, 33);
        this.lblStart.Name = "lblStart";
        this.lblStart.Size = new Size(71, 15);
        this.lblStart.Tag = "secondary";
        this.lblStart.Text = "Fecha inicio:";

        this.dtpStart.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.dtpStart.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.dtpStart.Format = DateTimePickerFormat.Short;
        this.dtpStart.Location = new Point(465, 30);
        this.dtpStart.Name = "dtpStart";
        this.dtpStart.Size = new Size(120, 23);

        this.lblEnd.AutoSize = true;
        this.lblEnd.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.lblEnd.Location = new Point(615, 33);
        this.lblEnd.Name = "lblEnd";
        this.lblEnd.Size = new Size(59, 15);
        this.lblEnd.Tag = "secondary";
        this.lblEnd.Text = "Fecha fin:";

        this.dtpEnd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.dtpEnd.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.dtpEnd.Format = DateTimePickerFormat.Short;
        this.dtpEnd.Location = new Point(690, 30);
        this.dtpEnd.Name = "dtpEnd";
        this.dtpEnd.Size = new Size(120, 23);

        this.lblTheme.AutoSize = true;
        this.lblTheme.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.lblTheme.Location = new Point(615, 70);
        this.lblTheme.Name = "lblTheme";
        this.lblTheme.Size = new Size(40, 15);
        this.lblTheme.Tag = "secondary";
        this.lblTheme.Text = "Tema:";

        this.cmbTheme.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cmbTheme.FormattingEnabled = true;
        this.cmbTheme.Items.AddRange(new object[] { "Claro", "Oscuro" });
        this.cmbTheme.Location = new Point(690, 66);
        this.cmbTheme.Name = "cmbTheme";
        this.cmbTheme.Size = new Size(120, 23);
        this.cmbTheme.SelectedIndexChanged += new EventHandler(this.cmbTheme_SelectedIndexChanged);

        this.grpUbicaciones.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.grpUbicaciones.BackColor = Color.White;
        this.grpUbicaciones.Controls.Add(this.lblOutput);
        this.grpUbicaciones.Controls.Add(this.txtOutputDirectory);
        this.grpUbicaciones.Controls.Add(this.btnBrowseOutput);
        this.grpUbicaciones.Controls.Add(this.lblTemplate);
        this.grpUbicaciones.Controls.Add(this.txtTemplatePath);
        this.grpUbicaciones.Controls.Add(this.btnBrowseTemplate);
        this.grpUbicaciones.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
        this.grpUbicaciones.ForeColor = Color.FromArgb(17, 37, 62);
        this.grpUbicaciones.Location = new Point(20, 210);
        this.grpUbicaciones.Name = "grpUbicaciones";
        this.grpUbicaciones.Size = new Size(940, 140);
        this.grpUbicaciones.TabStop = false;
        this.grpUbicaciones.Text = "Rutas";

        this.lblOutput.AutoSize = true;
        this.lblOutput.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.lblOutput.Location = new Point(16, 30);
        this.lblOutput.Name = "lblOutput";
        this.lblOutput.Size = new Size(94, 15);
        this.lblOutput.Tag = "secondary";
        this.lblOutput.Text = "Carpeta reportes:";

        this.txtOutputDirectory.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.txtOutputDirectory.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.txtOutputDirectory.Location = new Point(16, 49);
        this.txtOutputDirectory.Name = "txtOutputDirectory";
        this.txtOutputDirectory.Size = new Size(825, 23);
        this.tipMain.SetToolTip(this.txtOutputDirectory, "Carpeta donde se guardarán los reportes generados.");

        this.btnBrowseOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnBrowseOutput.Location = new Point(850, 48);
        this.btnBrowseOutput.Name = "btnBrowseOutput";
        this.btnBrowseOutput.Size = new Size(76, 25);
        this.btnBrowseOutput.Text = "Examinar";
        this.btnBrowseOutput.UseVisualStyleBackColor = true;
        this.btnBrowseOutput.Click += new EventHandler(this.btnBrowseOutput_Click);

        this.lblTemplate.AutoSize = true;
        this.lblTemplate.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.lblTemplate.Location = new Point(16, 84);
        this.lblTemplate.Name = "lblTemplate";
        this.lblTemplate.Size = new Size(131, 15);
        this.lblTemplate.Tag = "secondary";
        this.lblTemplate.Text = "Plantilla Excel (formato):";

        this.txtTemplatePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.txtTemplatePath.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.txtTemplatePath.Location = new Point(16, 103);
        this.txtTemplatePath.Name = "txtTemplatePath";
        this.txtTemplatePath.Size = new Size(825, 23);
        this.tipMain.SetToolTip(this.txtTemplatePath, "Plantilla de Excel usada para rellenar casillas del formato oficial.");

        this.btnBrowseTemplate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnBrowseTemplate.Location = new Point(850, 102);
        this.btnBrowseTemplate.Name = "btnBrowseTemplate";
        this.btnBrowseTemplate.Size = new Size(76, 25);
        this.btnBrowseTemplate.Text = "Examinar";
        this.btnBrowseTemplate.UseVisualStyleBackColor = true;
        this.btnBrowseTemplate.Click += new EventHandler(this.btnBrowseTemplate_Click);

        this.grpAcciones.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.grpAcciones.BackColor = Color.White;
        this.grpAcciones.Controls.Add(this.flpActionButtons);
        this.grpAcciones.Controls.Add(this.lblConnectionValue);
        this.grpAcciones.Controls.Add(this.lblConnectionTitle);
        this.grpAcciones.Controls.Add(this.pnlConnectionIndicator);
        this.grpAcciones.Controls.Add(this.progressAction);
        this.grpAcciones.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
        this.grpAcciones.ForeColor = Color.FromArgb(17, 37, 62);
        this.grpAcciones.Location = new Point(20, 359);
        this.grpAcciones.Name = "grpAcciones";
        this.grpAcciones.Size = new Size(940, 90);
        this.grpAcciones.TabStop = false;
        this.grpAcciones.Text = "Acciones";

        this.flpActionButtons.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.flpActionButtons.AutoScroll = true;
        this.flpActionButtons.FlowDirection = FlowDirection.LeftToRight;
        this.flpActionButtons.Location = new Point(16, 28);
        this.flpActionButtons.Name = "flpActionButtons";
        this.flpActionButtons.Size = new Size(720, 44);
        this.flpActionButtons.WrapContents = false;
        this.flpActionButtons.Controls.Add(this.btnGenerate);
        this.flpActionButtons.Controls.Add(this.btnPreview);
        this.flpActionButtons.Controls.Add(this.btnOpenReport);
        this.flpActionButtons.Controls.Add(this.btnTestConnection);
        this.flpActionButtons.Controls.Add(this.btnAdminSql);

        this.btnGenerate.BackColor = Color.FromArgb(0, 120, 215);
        this.btnGenerate.FlatStyle = FlatStyle.Flat;
        this.btnGenerate.ForeColor = Color.White;
        this.btnGenerate.Location = new Point(3, 3);
        this.btnGenerate.Margin = new Padding(0, 0, 8, 0);
        this.btnGenerate.Name = "btnGenerate";
        this.btnGenerate.Size = new Size(160, 36);
        this.btnGenerate.Text = "Generar reporte";
        this.btnGenerate.UseVisualStyleBackColor = false;
        this.btnGenerate.Click += new EventHandler(this.btnGenerate_Click);
        this.tipMain.SetToolTip(this.btnGenerate, "Genera reporte individual o masivo según filtros.");

        this.btnPreview.Location = new Point(171, 3);
        this.btnPreview.Margin = new Padding(0, 0, 8, 0);
        this.btnPreview.Name = "btnPreview";
        this.btnPreview.Size = new Size(150, 36);
        this.btnPreview.Text = "Vista previa";
        this.btnPreview.UseVisualStyleBackColor = true;
        this.btnPreview.Click += new EventHandler(this.btnPreview_Click);
        this.tipMain.SetToolTip(this.btnPreview, "Muestra una vista previa sin generar Excel.");

        this.btnOpenReport.Location = new Point(329, 3);
        this.btnOpenReport.Margin = new Padding(0, 0, 8, 0);
        this.btnOpenReport.Name = "btnOpenReport";
        this.btnOpenReport.Size = new Size(150, 36);
        this.btnOpenReport.Text = "Abrir último reporte";
        this.btnOpenReport.UseVisualStyleBackColor = true;
        this.btnOpenReport.Enabled = false;
        this.btnOpenReport.Click += new EventHandler(this.btnOpenReport_Click);

        this.btnTestConnection.Location = new Point(487, 3);
        this.btnTestConnection.Margin = new Padding(0, 0, 8, 0);
        this.btnTestConnection.Name = "btnTestConnection";
        this.btnTestConnection.Size = new Size(150, 36);
        this.btnTestConnection.Text = "Probar conexión";
        this.btnTestConnection.UseVisualStyleBackColor = true;
        this.btnTestConnection.Click += new EventHandler(this.btnTestConnection_Click);
        this.tipMain.SetToolTip(this.btnTestConnection, "Valida conexión activa (directo o fallback) y actualiza indicador.");

        this.btnAdminSql.Location = new Point(645, 3);
        this.btnAdminSql.Margin = new Padding(0, 0, 8, 0);
        this.btnAdminSql.Name = "btnAdminSql";
        this.btnAdminSql.Size = new Size(95, 36);
        this.btnAdminSql.Text = "Admin SQL";
        this.btnAdminSql.UseVisualStyleBackColor = true;
        this.btnAdminSql.Click += new EventHandler(this.btnAdminSql_Click);
        this.tipMain.SetToolTip(this.btnAdminSql, "Abrir módulo de sincronización SQL, catálogo e historial.");

        this.lblConnectionTitle.AutoSize = true;
        this.lblConnectionTitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.lblConnectionTitle.Location = new Point(746, 30);
        this.lblConnectionTitle.Name = "lblConnectionTitle";
        this.lblConnectionTitle.Size = new Size(62, 15);
        this.lblConnectionTitle.Tag = "secondary";
        this.lblConnectionTitle.Text = "Conexión:";

        this.pnlConnectionIndicator.BackColor = Color.FromArgb(220, 53, 69);
        this.pnlConnectionIndicator.BorderStyle = BorderStyle.FixedSingle;
        this.pnlConnectionIndicator.Location = new Point(814, 33);
        this.pnlConnectionIndicator.Name = "pnlConnectionIndicator";
        this.pnlConnectionIndicator.Size = new Size(14, 14);

        this.lblConnectionValue.Location = new Point(830, 27);
        this.lblConnectionValue.Name = "lblConnectionValue";
        this.lblConnectionValue.Size = new Size(96, 24);
        this.lblConnectionValue.Tag = "semantic";
        this.lblConnectionValue.Text = "Verificando...";

        this.progressAction.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.progressAction.Location = new Point(744, 58);
        this.progressAction.Name = "progressAction";
        this.progressAction.Size = new Size(182, 14);
        this.progressAction.Style = ProgressBarStyle.Marquee;
        this.progressAction.Visible = false;

        this.connectionTimer.Interval = 30000;
        this.connectionTimer.Tick += new EventHandler(this.connectionTimer_Tick);

        this.grpEstado.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.grpEstado.BackColor = Color.White;
        this.grpEstado.Controls.Add(this.lblStatusCaption);
        this.grpEstado.Controls.Add(this.txtStatus);
        this.grpEstado.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
        this.grpEstado.ForeColor = Color.FromArgb(17, 37, 62);
        this.grpEstado.Location = new Point(20, 457);
        this.grpEstado.Name = "grpEstado";
        this.grpEstado.Size = new Size(940, 80);
        this.grpEstado.TabStop = false;
        this.grpEstado.Text = "Estado";

        this.lblStatusCaption.AutoSize = true;
        this.lblStatusCaption.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.lblStatusCaption.Location = new Point(16, 25);
        this.lblStatusCaption.Name = "lblStatusCaption";
        this.lblStatusCaption.Size = new Size(114, 15);
        this.lblStatusCaption.Tag = "secondary";
        this.lblStatusCaption.Text = "Bitácora de eventos:";

        this.txtStatus.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.txtStatus.BackColor = Color.FromArgb(247, 250, 255);
        this.txtStatus.BorderStyle = BorderStyle.None;
        this.txtStatus.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        this.txtStatus.Location = new Point(16, 43);
        this.txtStatus.Name = "txtStatus";
        this.txtStatus.ReadOnly = true;
        this.txtStatus.ScrollBars = RichTextBoxScrollBars.Vertical;
        this.txtStatus.Size = new Size(910, 30);

        this.Controls.Add(this.pnlTopBar);
        this.Controls.Add(this.grpFiltros);
        this.Controls.Add(this.grpUbicaciones);
        this.Controls.Add(this.grpAcciones);
        this.Controls.Add(this.grpEstado);

        this.pnlTopBar.ResumeLayout(false);
        this.pnlTopBar.PerformLayout();
        this.grpFiltros.ResumeLayout(false);
        this.grpFiltros.PerformLayout();
        this.grpUbicaciones.ResumeLayout(false);
        this.grpUbicaciones.PerformLayout();
        this.grpAcciones.ResumeLayout(false);
        this.grpEstado.ResumeLayout(false);
        this.grpEstado.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private Panel pnlTopBar;
    private Label lblAppGlyph;
    private Label lblTitle;
    private Label lblSubtitle;
    private RoundedGroupBox grpFiltros;
    private Label lblUserId;
    private ComboBox cmbEmployee;
    private CheckBox chkAllEmployees;
    private Label lblStart;
    private DateTimePicker dtpStart;
    private Label lblEnd;
    private DateTimePicker dtpEnd;
    private Label lblTheme;
    private ComboBox cmbTheme;
    private RoundedGroupBox grpUbicaciones;
    private Label lblOutput;
    private TextBox txtOutputDirectory;
    private Button btnBrowseOutput;
    private Label lblTemplate;
    private TextBox txtTemplatePath;
    private Button btnBrowseTemplate;
    private RoundedGroupBox grpAcciones;
    private FlowLayoutPanel flpActionButtons;
    private Button btnGenerate;
    private Button btnPreview;
    private Button btnOpenReport;
    private Button btnTestConnection;
    private Button btnAdminSql;
    private Label lblConnectionTitle;
    private Panel pnlConnectionIndicator;
    private StatusPillLabel lblConnectionValue;
    private ProgressBar progressAction;
    private RoundedGroupBox grpEstado;
    private Label lblStatusCaption;
    private RichTextBox txtStatus;
    private ToolTip tipMain;
    private System.Windows.Forms.Timer connectionTimer;

    #endregion
}
