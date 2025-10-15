using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Controls;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using ShalevOhad.DCS.SRS.Recorder.Core.Analysis;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Components;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Tabs
{
    /// <summary>
    /// Component-based general tab that combines file management, audio playback, and analysis functionality
    /// This is the new modular implementation that replaces the monolithic design.
    /// </summary>
    public partial class GeneralTabView : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Services
        
        private IRecordingInfoService? _recordingInfoService;
        private IAudioPlaybackService? _audioPlaybackService;
        private ISettingsService? _settingsService;
        private IUIService? _uiService;
        private IWaveformService? _waveformService;
        
        // Enhanced services
        private IAnalysisVisualizationService? _analysisVisualizationService;
        private IFrequencyTreeService? _frequencyTreeService;
        private IRecentFilesService? _recentFilesService;
        private IBookmarkService? _bookmarkService;
        private ILiveAnalysisService? _liveAnalysisService;
        
        #endregion

        #region Component-Based Layout
        
        private Panel _mainPanel;
        private FileSelectionComponent _fileSelectionComponent;
        private TabControl _functionalityTabControl;
        private PlayerComponent _playerComponent;
        private AnalyzerComponent _analyzerComponent;
        
        #endregion

        #region Data and State
        
        private string? _currentFilePath;
        
        #endregion

        #region Events
        
        public event EventHandler<string>? StatusChanged;
        
        #endregion

        public GeneralTabView()
        {
            InitializeComponent();
            CreateMainLayout();
            // Note: SetupEventHandlers() will be called after components are initialized in Initialize() method
        }

        public void Initialize(
            IRecordingInfoService recordingInfoService,
            IAudioPlaybackService audioPlaybackService,
            ISettingsService settingsService,
            IUIService uiService,
            IWaveformService waveformService)
        {
            _recordingInfoService = recordingInfoService ?? throw new ArgumentNullException(nameof(recordingInfoService));
            _audioPlaybackService = audioPlaybackService ?? throw new ArgumentNullException(nameof(audioPlaybackService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            _waveformService = waveformService ?? throw new ArgumentNullException(nameof(waveformService));

            // Check if components were successfully created during layout initialization
            if (_playerComponent == null || _analyzerComponent == null)
            {
                Logger.Error("Components were not properly created during layout initialization. Attempting to recreate...");
                
                // Try to recreate the functionality tabs
                try
                {
                    // Remove any existing error labels first
                    var errorControls = _mainPanel.Controls.OfType<Label>()
                        .Where(l => l.BackColor == Color.LightYellow || l.ForeColor == Color.Red)
                        .ToList();
                    
                    foreach (var errorControl in errorControls)
                    {
                        _mainPanel.Controls.Remove(errorControl);
                        errorControl.Dispose();
                    }
                    
                    CreateFunctionalityTabs(); // Retry component creation
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to recreate components during Initialize");
                }
                
                // Final check - if components are still null, throw a descriptive error
                if (_playerComponent == null || _analyzerComponent == null)
                {
                    var missingComponents = new List<string>();
                    if (_playerComponent == null) missingComponents.Add("PlayerComponent");
                    if (_analyzerComponent == null) missingComponents.Add("AnalyzerComponent");
                    
                    throw new InvalidOperationException(
                        $"Failed to create required components: {string.Join(", ", missingComponents)}. " +
                        "Check the application logs for component creation errors. " +
                        "This may be caused by missing dependencies or constructor failures in the component classes.");
                }
            }

            // Initialize components with services
            try
            {
                _playerComponent.Initialize(audioPlaybackService, waveformService, uiService);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize PlayerComponent");
                throw new InvalidOperationException($"Failed to initialize PlayerComponent: {ex.Message}", ex);
            }
            
            try
            {
                _analyzerComponent.Initialize(uiService);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize AnalyzerComponent");
                throw new InvalidOperationException($"Failed to initialize AnalyzerComponent: {ex.Message}", ex);
            }
            
            try
            {
                _fileSelectionComponent.Initialize(uiService, settingsService);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize FileSelectionComponent");
                throw new InvalidOperationException($"Failed to initialize FileSelectionComponent: {ex.Message}", ex);
            }

            // Setup event handlers after components are initialized
            try
            {
                SetupEventHandlers();
                SetupServiceEventHandlers();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to setup event handlers");
                // Don't throw here as this is not critical for basic functionality
            }
            
            // Load the last file if available
            var lastFilePath = _settingsService.LastFilePath;
            if (!string.IsNullOrEmpty(lastFilePath) && File.Exists(lastFilePath))
            {
                _ = LoadFileAsync(lastFilePath);
            }
        }

        #region Main Layout Creation

        private void CreateMainLayout()
        {
            try
            {
                Logger.Info("Creating main layout...");
                
                // Main panel
                _mainPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(8),
                    BackColor = SystemColors.Control, // Add background color for visibility
                    MinimumSize = new Size(800, 600)  // Ensure minimum size for the entire tab
                };
                Controls.Add(_mainPanel);
                Logger.Info($"Main panel created. Size: {_mainPanel.Size}");

                // Create components in proper order for docking
                // IMPORTANT: In WinForms, when mixing DockStyle.Fill with other dock styles,
                // the Fill control should be added LAST, after other docked controls
                CreateFileSelectionComponent(); // DockStyle.Top - add first
                CreateFunctionalityTabs();   // DockStyle.Fill - add last
                
                Logger.Info($"Main layout created. Main panel has {_mainPanel.Controls.Count} controls");
                
                // Force layout refresh
                _mainPanel.PerformLayout();
                PerformLayout();
                
                Logger.Info("Layout refresh completed");
                
                // Validate components for debugging
                ValidateComponents();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error creating main layout");
                
                // Create a fallback error display
                var errorPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Red
                };
                var errorLabel = new Label
                {
                    Text = $"Critical Error: {ex.Message}",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.White,
                    Font = new Font(Font, FontStyle.Bold)
                };
                errorPanel.Controls.Add(errorLabel);
                Controls.Add(errorPanel);
            }
        }

        private void CreateFileSelectionComponent()
        {
            try
            {
                Logger.Info("Creating file selection component...");
                
                _fileSelectionComponent = new FileSelectionComponent
                {
                    Height = 55,
                    Dock = DockStyle.Top,
                    BackColor = SystemColors.Control,
                    MinimumSize = new Size(400, 55),  // Ensure minimum width
                    MaximumSize = new Size(0, 55)      // Fixed height
                };
                _mainPanel.Controls.Add(_fileSelectionComponent);
                
                Logger.Info($"File selection component created. Size: {_fileSelectionComponent.Size}, Location: {_fileSelectionComponent.Location}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error creating file selection component");
                
                // Fallback: create a simple panel
                var fallbackPanel = new Panel
                {
                    Height = 55,
                    Dock = DockStyle.Top,
                    BackColor = Color.LightCoral,
                    BorderStyle = BorderStyle.FixedSingle
                };
                var errorLabel = new Label
                {
                    Text = $"File Selection Error: {ex.Message}",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.White
                };
                fallbackPanel.Controls.Add(errorLabel);
                _mainPanel.Controls.Add(fallbackPanel);
            }
        }

        private void CreateFunctionalityTabs()
        {
            try
            {
                Logger.Info("Creating functionality tabs...");
                
                Logger.Info("Creating TabControl...");
                _functionalityTabControl = new TabControl
                {
                    Dock = DockStyle.Fill,
                    Padding = new Point(8, 4),
                    Appearance = TabAppearance.Normal,
                    SizeMode = TabSizeMode.Normal,
                    BackColor = SystemColors.Control, // Add background color for visibility
                    MinimumSize = new Size(400, 300)   // Ensure minimum size
                };
                Logger.Info("TabControl created successfully");

                // Create player component
                Logger.Info("Creating player tab page...");
                var playerTabPage = new TabPage("Audio Player")
                {
                    UseVisualStyleBackColor = true,
                    ToolTipText = "Audio playback controls and waveform display",
                    BackColor = SystemColors.Window,
                    Padding = new Padding(3)
                };
                Logger.Info("Player tab page created successfully");

                Logger.Info("Creating PlayerComponent...");
                try
                {
                    _playerComponent = new PlayerComponent();
                    Logger.Info("PlayerComponent constructor completed");
                    
                    _playerComponent.Dock = DockStyle.Fill;
                    _playerComponent.BackColor = SystemColors.Window;
                    _playerComponent.MinimumSize = new Size(300, 200);
                    Logger.Info("PlayerComponent properties set successfully");
                    
                    playerTabPage.Controls.Add(_playerComponent);
                    Logger.Info("PlayerComponent added to tab page successfully");
                }
                catch (Exception playerEx)
                {
                    Logger.Error(playerEx, "Failed to create PlayerComponent");
                    throw new InvalidOperationException($"PlayerComponent creation failed: {playerEx.Message}", playerEx);
                }

                // Create analyzer component
                Logger.Info("Creating analyzer tab page...");
                var analyzerTabPage = new TabPage("File Analyzer")
                {
                    UseVisualStyleBackColor = true,
                    ToolTipText = "Recording file analysis tools",
                    BackColor = SystemColors.Window,
                    Padding = new Padding(3)
                };
                Logger.Info("Analyzer tab page created successfully");

                Logger.Info("Creating AnalyzerComponent...");
                try
                {
                    _analyzerComponent = new AnalyzerComponent();
                    Logger.Info("AnalyzerComponent constructor completed");
                    
                    _analyzerComponent.Dock = DockStyle.Fill;
                    _analyzerComponent.BackColor = SystemColors.Window;
                    _analyzerComponent.MinimumSize = new Size(300, 200);
                    Logger.Info("AnalyzerComponent properties set successfully");
                    
                    analyzerTabPage.Controls.Add(_analyzerComponent);
                    Logger.Info("AnalyzerComponent added to tab page successfully");
                }
                catch (Exception analyzerEx)
                {
                    Logger.Error(analyzerEx, "Failed to create AnalyzerComponent");
                    throw new InvalidOperationException($"AnalyzerComponent creation failed: {analyzerEx.Message}", analyzerEx);
                }

                Logger.Info("Adding tab pages to TabControl...");
                _functionalityTabControl.TabPages.Add(playerTabPage);
                _functionalityTabControl.TabPages.Add(analyzerTabPage);
                Logger.Info("Tab pages added successfully");

                // Set the first tab as selected
                Logger.Info("Setting selected tab index...");
                _functionalityTabControl.SelectedIndex = 0;
                Logger.Info("Selected tab index set successfully");

                Logger.Info("Adding TabControl to main panel...");
                _mainPanel.Controls.Add(_functionalityTabControl);
                Logger.Info("TabControl added to main panel successfully");
                
                // Force visibility and refresh
                Logger.Info("Setting TabControl visibility...");
                _functionalityTabControl.Visible = true;
                _functionalityTabControl.BringToFront();
                Logger.Info("TabControl visibility set successfully");
                
                // Ensure tab pages are visible
                Logger.Info("Validating tab page visibility...");
                foreach (TabPage page in _functionalityTabControl.TabPages)
                {
                    page.Visible = true;
                    Logger.Info($"Tab page '{page.Text}' visible: {page.Visible}, controls: {page.Controls.Count}");
                }
                
                Logger.Info($"Functionality tabs created successfully. Tab count: {_functionalityTabControl.TabPages.Count}");
                Logger.Info($"TabControl size: {_functionalityTabControl.Size}, Location: {_functionalityTabControl.Location}");
                Logger.Info($"TabControl visible: {_functionalityTabControl.Visible}");
                Logger.Info($"PlayerComponent null check: {(_playerComponent == null ? "NULL" : "OK")}");
                Logger.Info($"AnalyzerComponent null check: {(_analyzerComponent == null ? "NULL" : "OK")}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error creating functionality tabs");
                
                // Ensure components are null if creation failed
                _playerComponent = null;
                _analyzerComponent = null;
                
                // Fallback: create a simple label to show there's an issue
                var errorLabel = new Label
                {
                    Text = $"Error creating components: {ex.Message}\n\n{ex.GetType().Name}\n\nClick to retry",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Red,
                    BackColor = Color.LightYellow,
                    BorderStyle = BorderStyle.FixedSingle
                };
                errorLabel.Click += (s, e) => {
                    _mainPanel.Controls.Remove(errorLabel);
                    CreateFunctionalityTabs();
                };
                _mainPanel.Controls.Add(errorLabel);
            }
        }

        #endregion

        #region Event Handlers Setup

        private void SetupEventHandlers()
        {
            // File selection events
            _fileSelectionComponent.FileSelected += OnFileSelected;

            // Player component events
            _playerComponent.StatusChanged += OnComponentStatusChanged;

            // Analyzer component events
            _analyzerComponent.StatusChanged += OnComponentStatusChanged;
            _analyzerComponent.AnalysisStarted += OnAnalysisStarted;
            _analyzerComponent.AnalysisCompleted += OnAnalysisCompleted;
        }

        private void SetupServiceEventHandlers()
        {
            if (_audioPlaybackService != null)
            {
                _audioPlaybackService.PlaybackStarted += OnPlaybackStarted;
                _audioPlaybackService.PlaybackStopped += OnPlaybackStopped;
                _audioPlaybackService.PlaybackPaused += OnPlaybackPaused;
                _audioPlaybackService.PlaybackResumed += OnPlaybackResumed;
                _audioPlaybackService.PlaybackError += OnPlaybackError;
                // _audioPlaybackService.PlaybackStateChanged += OnPlaybackStateChanged; // TODO: Implement when PlaybackState is available
            }
        }

        #endregion

        #region File Management

        public async Task LoadFileAsync(string filePath)
        {
            try
            {
                OnStatusChanged("Loading file...");

                var recordingInfo = await _recordingInfoService?.LoadRecordingInfoAsync(filePath)!;

                if (!recordingInfo.IsValid)
                {
                    _uiService?.ShowError("The selected file is not a valid SRS recording.");
                    return;
                }

                _currentFilePath = filePath;

                // Update file selection component
                _fileSelectionComponent.SetCurrentFile(filePath);

                // Update components with new file data
                await _playerComponent.LoadFileAsync(filePath, recordingInfo);
                await _analyzerComponent.LoadFileAsync(filePath, recordingInfo);

                // Update status
                OnStatusChanged($"Loaded: {Path.GetFileName(filePath)} | {recordingInfo.FormattedInfo}");

                // Save as last used file
                if (_settingsService != null)
                {
                    _settingsService.LastFilePath = filePath;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading file: {filePath}", filePath);
                _uiService?.ShowError($"Error loading file: {ex.Message}");
            }
        }

        private async void OnFileSelected(object? sender, string filePath)
        {
            await LoadFileAsync(filePath);
        }

        #endregion

        #region Component Event Handlers

        private void OnComponentStatusChanged(object? sender, string status)
        {
            OnStatusChanged(status);
        }

        private void OnAnalysisStarted(object? sender, EventArgs e)
        {
            OnStatusChanged("Analysis in progress...");
        }

        private void OnAnalysisCompleted(object? sender, string results)
        {
            OnStatusChanged("Analysis completed");
        }

        #endregion

        #region Service Event Handlers

        private void OnPlaybackStarted(object? sender, EventArgs e)
        {
            OnStatusChanged("Playing");
        }

        private void OnPlaybackStopped(object? sender, EventArgs e)
        {
            OnStatusChanged("Stopped");
        }

        private void OnPlaybackPaused(object? sender, EventArgs e)
        {
            OnStatusChanged("Paused");
        }

        private void OnPlaybackResumed(object? sender, EventArgs e)
        {
            OnStatusChanged("Playing");
        }

        private void OnPlaybackError(object? sender, Exception e)
        {
            OnStatusChanged($"Error: {e.Message}");
        }

        private void OnPlaybackStateChanged(object? sender, EventArgs e)
        {
            // Components handle their own state updates
        }

        #endregion

        #region Helper Methods

        private static string FormatTime(TimeSpan time)
        {
            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
                : $"{time.Minutes}:{time.Seconds:D2}";
        }

        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        /// <summary>
        /// Validates that all components are properly created and configured
        /// </summary>
        private void ValidateComponents()
        {
            Logger.Info("=== Component Validation ===");
            Logger.Info($"Main panel: {(_mainPanel != null ? "Created" : "NULL")}");
            Logger.Info($"File selection component: {(_fileSelectionComponent != null ? "Created" : "NULL")}");
            Logger.Info($"Tab control: {(_functionalityTabControl != null ? "Created" : "NULL")}");
            Logger.Info($"Player component: {(_playerComponent != null ? "Created" : "NULL")}");
            Logger.Info($"Analyzer component: {(_analyzerComponent != null ? "Created" : "NULL")}");
            
            if (_functionalityTabControl != null)
            {
                Logger.Info($"Tab control tab count: {_functionalityTabControl.TabPages.Count}");
                Logger.Info($"Tab control visible: {_functionalityTabControl.Visible}");
                Logger.Info($"Tab control size: {_functionalityTabControl.Size}");
                Logger.Info($"Selected tab index: {_functionalityTabControl.SelectedIndex}");
            }
            
            Logger.Info("=== End Component Validation ===");
        }

        /// <summary>
        /// Gets the PlayerComponent for external access (used for saving settings)
        /// </summary>
        public PlayerComponent? GetPlayerComponent()
        {
            return _playerComponent;
        }

        #endregion

        private void InitializeComponent()
        {
            SuspendLayout();
            
            Name = "GeneralTabView";
            Size = new Size(800, 500);
            
            ResumeLayout(false);
        }
    }
}