using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Components
{
    /// <summary>
    /// Component for file selection and management
    /// </summary>
    public partial class FileSelectionComponent : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Controls
        
        private Panel _containerPanel;
        private TableLayoutPanel _layoutPanel;
        private Label _fileSelectionLabel;
        private TextBox _filePathTextBox;
        private Button _browseButton;
        
        #endregion

        #region Services
        
        private IUIService? _uiService;
        private ISettingsService? _settingsService;
        
        #endregion

        #region Events
        
        public event EventHandler<string>? FileSelected;
        
        #endregion

        public FileSelectionComponent()
        {
            InitializeComponent();
            CreateControls();
            SetupEventHandlers();
        }

        public void Initialize(IUIService uiService, ISettingsService settingsService)
        {
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public void SetCurrentFile(string filePath)
        {
            _filePathTextBox.Text = filePath;
        }

        #region Control Creation

        private void CreateControls()
        {
            _containerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8),
                Margin = new Padding(0, 0, 0, 8)
            };
            Controls.Add(_containerPanel);

            _layoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _layoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _containerPanel.Controls.Add(_layoutPanel);

            CreateLabel();
            CreateTextBox();
            CreateBrowseButton();
        }

        private void CreateLabel()
        {
            _fileSelectionLabel = new Label
            {
                Text = "Recording File:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 12, 0),
                Font = new Font(Font, FontStyle.Bold)
            };
            _layoutPanel.Controls.Add(_fileSelectionLabel, 0, 0);
        }

        private void CreateTextBox()
        {
            _filePathTextBox = new TextBox
            {
                ReadOnly = true,
                BackColor = SystemColors.Window,
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                Margin = new Padding(0, 0, 12, 0)
            };
            _layoutPanel.Controls.Add(_filePathTextBox, 1, 0);
        }

        private void CreateBrowseButton()
        {
            _browseButton = new Button
            {
                Text = "Browse...",
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(0, 0, 0, 0)
            };
            _layoutPanel.Controls.Add(_browseButton, 2, 0);
        }

        #endregion

        #region Event Handlers

        private void SetupEventHandlers()
        {
            _browseButton.Click += OnBrowseButtonClick;
        }

        private async void OnBrowseButtonClick(object? sender, EventArgs e)
        {
            try
            {
                var filePath = await _uiService?.ShowOpenFileDialogAsync(
                    "SRS Recording Files (*.raw)|*.raw|All Files (*.*)|*.*",
                    "Open SRS Recording")!;

                if (!string.IsNullOrEmpty(filePath))
                {
                    FileSelected?.Invoke(this, filePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening file");
                _uiService?.ShowError($"Error opening file: {ex.Message}");
            }
        }

        #endregion

        private void InitializeComponent()
        {
            SuspendLayout();
            
            Name = "FileSelectionComponent";
            Size = new Size(600, 55);
            
            ResumeLayout(false);
        }
    }
}