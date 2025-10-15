using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Infrastructure;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Tabs;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.ViewModels;
using ShalevOhad.DCS.SRS.Recorder.Core.Settings;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views
{
    /// <summary>
    /// Modernized main player form using modular tab architecture
    /// </summary>
    public partial class MainPlayerForm : Form
    {
        #region Constants and Static Fields
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private const int DefaultFormWidth = 1200;
        private const int DefaultFormHeight = 900;
        private const int MinimumFormWidth = 900;
        private const int MinimumFormHeight = 700;
        
        #endregion

        #region Services and Core Components
        
        private ServiceContainer? _serviceContainer;
        private MainPlayerPresenter? _presenter;
        private ISettingsService? _settingsService;
        private IUIService? _uiService;
        private PlayerSettingsStore _playerSettings;
        
        private bool _isInitialized;
        private bool _isDisposing;
        
        #endregion

        #region UI Components - Main Structure
        
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _statusLabel = null!;
        private ToolStripProgressBar _progressBar = null!;
        private TabControl _tabControl = null!;
        
        #endregion



        #region UI Components - Tab Views
        
        private GeneralTabView _generalTabView = null!;
        private AudioTestTabView _audioTestTabView = null!;
        
        #endregion
        
        
        #region Constructor and Initialization
        
        public MainPlayerForm()
        {
            try
            {
                Logger.Info("Initializing MainPlayerForm with modular architecture");
                
                _playerSettings = PlayerSettingsStore.Instance;
                
                InitializeComponent();
                InitializeFormProperties();
                LoadWindowSettings();
                CreateUIComponents();
                SetupBaseEventHandlers();
                
                Logger.Info("MainPlayerForm basic initialization completed");
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Fatal error during MainPlayerForm construction");
                
                // Show a detailed error message for debugging
                MessageBox.Show(
                    $"Fatal error during MainPlayerForm construction:\n\n{ex.Message}\n\nInner exception: {ex.InnerException?.Message}",
                    "Initialization Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                
                throw;
            }
        }

        /// <summary>
        /// Initializes the form with the service container and completes the setup
        /// </summary>
        /// <param name="serviceContainer">The service container with all required services</param>
        /// <exception cref="ArgumentNullException">Thrown when serviceContainer is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when already initialized</exception>
        public async Task InitializeAsync(ServiceContainer serviceContainer)
        {
            if (_isInitialized)
                throw new InvalidOperationException("MainPlayerForm is already initialized");
                
            _serviceContainer = serviceContainer ?? throw new ArgumentNullException(nameof(serviceContainer));

            try
            {
                Logger.Info("Starting full initialization with services");

                // Get core services
                await InitializeServicesAsync();

                // Create and configure presenter
                await InitializePresenterAsync();

                // Initialize all tab views
                await InitializeTabViewsAsync();

                // Setup cross-component event handlers
                SetupAdvancedEventHandlers();

                _isInitialized = true;
                Logger.Info("MainPlayerForm fully initialized with all services and tabs");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during full initialization");
                
                // Reset initialization state on failure
                _isInitialized = false;
                _serviceContainer = null;
                
                // Show detailed error information
                var errorMessage = $"Failed to initialize application:\n\n{ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\nInner exception: {ex.InnerException.Message}";
                }
                
                MessageBox.Show(
                    errorMessage,
                    "Initialization Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                
                throw;
            }
        }
        
        #endregion

        #region Core Initialization Methods
        
        private void InitializeFormProperties()
        {
            Text = "DCS SRS Recording Player";
            Size = new Size(DefaultFormWidth, DefaultFormHeight);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(MinimumFormWidth, MinimumFormHeight);
            Icon = LoadApplicationIcon();
        }

        private void LoadWindowSettings()
        {
            try
            {
                // Load window position and size
                var windowX = _playerSettings.GetPlayerSettingInt(PlayerSettingKeys.WindowX);
                var windowY = _playerSettings.GetPlayerSettingInt(PlayerSettingKeys.WindowY);
                var windowWidth = _playerSettings.GetPlayerSettingInt(PlayerSettingKeys.WindowWidth);
                var windowHeight = _playerSettings.GetPlayerSettingInt(PlayerSettingKeys.WindowHeight);

                // Apply size if valid
                if (windowWidth >= MinimumFormWidth && windowHeight >= MinimumFormHeight)
                {
                    Size = new Size(windowWidth, windowHeight);
                }

                // Apply position if valid (not -1, which means center)
                if (windowX >= 0 && windowY >= 0)
                {
                    // Ensure the window is visible on screen
                    var screen = Screen.FromPoint(new Point(windowX, windowY));
                    if (screen.WorkingArea.Contains(windowX, windowY))
                    {
                        StartPosition = FormStartPosition.Manual;
                        Location = new Point(windowX, windowY);
                    }
                }

                Logger.Debug($"Loaded window settings - Position: ({windowX}, {windowY}), Size: {windowWidth}x{windowHeight}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading window settings");
            }
        }

        private void SaveWindowSettings()
        {
            try
            {
                // Only save if the form is in a normal window state
                if (WindowState == FormWindowState.Normal)
                {
                    _playerSettings.SaveWindowSettings(Location.X, Location.Y, Size.Width, Size.Height);
                    Logger.Debug($"Saved window settings - Position: ({Location.X}, {Location.Y}), Size: {Size.Width}x{Size.Height}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving window settings");
            }
        }

        private void CreateUIComponents()
        {
            try
            {
                Logger.Debug("Creating main UI components");
                
                CreateStatusStrip();
                CreateMainLayout();
                
                Logger.Debug("UI components created successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error creating UI components");
                throw;
            }
        }

        private async Task InitializeServicesAsync()
        {
            _settingsService = _serviceContainer!.GetService<ISettingsService>()
                ?? throw new InvalidOperationException("ISettingsService not found in container");
                
            _uiService = _serviceContainer.GetService<IUIService>()
                ?? throw new InvalidOperationException("IUIService not found in container");

            Logger.Debug("Core services initialized");
        }

        private async Task InitializePresenterAsync()
        {
            try
            {
                var recordingInfoService = _serviceContainer!.GetService<IRecordingInfoService>()
                    ?? throw new InvalidOperationException("IRecordingInfoService not found");
                var audioPlaybackService = _serviceContainer.GetService<IAudioPlaybackService>()
                    ?? throw new InvalidOperationException("IAudioPlaybackService not found");
                var waveformService = _serviceContainer.GetService<IWaveformService>()
                    ?? throw new InvalidOperationException("IWaveformService not found");

                _presenter = new MainPlayerPresenter(
                    recordingInfoService,
                    audioPlaybackService,
                    _settingsService!,
                    _uiService!,
                    waveformService
                );

                Logger.Debug("Presenter initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing presenter");
                throw;
            }
        }

        private async Task InitializeTabViewsAsync()
        {
            await InitializeGeneralTabAsync();
            await InitializeAudioTestTabAsync();
            
            Logger.Debug("All tab views initialized");
        }
        
        #endregion

        #region UI Creation Methods
        


        private void CreateStatusStrip()
        {
            _statusStrip = new StatusStrip();
            
            _statusLabel = new ToolStripStatusLabel("Ready")
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            _progressBar = new ToolStripProgressBar()
            {
                Visible = false,
                Size = new Size(200, 16)
            };

            _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _progressBar });
            Controls.Add(_statusStrip);
        }

        private void CreateMainLayout()
        {
            _tabControl = new TabControl()
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 6),
                SelectedIndex = 0,
                Appearance = TabAppearance.Normal,
                SizeMode = TabSizeMode.Normal
            };
            
            _tabControl.SelectedIndexChanged += OnTabChanged;
            Controls.Add(_tabControl);

            CreateTabPlaceholders();
        }

        private void CreateTabPlaceholders()
        {
            var generalTab = new TabPage("General")
            {
                UseVisualStyleBackColor = true,
                ToolTipText = "Audio playback and file analysis"
            };
            
            var audioTestTab = new TabPage("Audio Test")
            {
                UseVisualStyleBackColor = true,
                ToolTipText = "Audio system testing and diagnostics"
            };

            _tabControl.TabPages.AddRange(new TabPage[] { generalTab, audioTestTab });
        }
        
        private Icon? LoadApplicationIcon()
        {
            try
            {
                // Try to load application icon from resources or embedded resource
                // Return null if not found - form will use default icon
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Could not load application icon");
                return null;
            }
        }
        
        #endregion

        #region Tab Initialization Methods
        
        private async Task InitializeGeneralTabAsync()
        {
            try
            {
                var recordingInfoService = _serviceContainer!.GetService<IRecordingInfoService>()
                    ?? throw new InvalidOperationException("IRecordingInfoService not found");
                var audioPlaybackService = _serviceContainer.GetService<IAudioPlaybackService>()
                    ?? throw new InvalidOperationException("IAudioPlaybackService not found");
                var waveformService = _serviceContainer.GetService<IWaveformService>()
                    ?? throw new InvalidOperationException("IWaveformService not found");

                _generalTabView = new GeneralTabView();
                _generalTabView.Initialize(
                    recordingInfoService,
                    audioPlaybackService,
                    _settingsService!,
                    _uiService!,
                    waveformService);

                _generalTabView.StatusChanged += OnTabStatusChanged;

                _tabControl.TabPages[0].Controls.Clear();
                _generalTabView.Dock = DockStyle.Fill;
                _tabControl.TabPages[0].Controls.Add(_generalTabView);
                
                Logger.Debug("General tab initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing general tab");
                throw;
            }
        }

        private async Task InitializeAudioTestTabAsync()
        {
            try
            {
                var audioPlaybackService = _serviceContainer!.GetService<IAudioPlaybackService>()
                    ?? throw new InvalidOperationException("IAudioPlaybackService not found");

                _audioTestTabView = new AudioTestTabView();
                _audioTestTabView.Initialize(audioPlaybackService, _uiService!);
                _audioTestTabView.StatusChanged += OnTabStatusChanged;

                _tabControl.TabPages[1].Controls.Clear();
                _audioTestTabView.Dock = DockStyle.Fill;
                _tabControl.TabPages[1].Controls.Add(_audioTestTabView);
                
                Logger.Debug("Audio test tab initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing audio test tab");
                throw;
            }
        }
        
        #endregion

        #region Event Handler Setup
        
        private void SetupBaseEventHandlers()
        {
            FormClosing += OnFormClosing;
            Load += OnFormLoad;
            KeyPreview = true;
            KeyDown += OnFormKeyDown;
        }

        private void SetupAdvancedEventHandlers()
        {
            if (_presenter != null)
            {
                _presenter.StatusChanged += OnTabStatusChanged;
                _presenter.LoadingStateChanged += OnLoadingStateChanged;
            }
        }
        
        #endregion

        #region Event Handlers - Form Lifecycle
        
        private async void OnFormLoad(object? sender, EventArgs e)
        {
            try
            {
                // Check if we need to initialize with services from the factory
                if (!_isInitialized && Tag is ServiceContainer serviceContainer)
                {
                    Logger.Info("Initializing form with services from factory");
                    UpdateStatus("Initializing services...");
                    
                    await InitializeAsync(serviceContainer);
                    Tag = null; // Clear the tag after initialization
                }

                if (!_isInitialized)
                {
                    Logger.Warn("Form loaded but not fully initialized with services");
                    UpdateStatus("Waiting for initialization...");
                    return;
                }

                UpdateStatus("Loading application...");
                
                await _presenter?.InitializeAsync()!;
                await _presenter?.LoadLastFileAsync()!;

                // Initialize audio devices for the test tab
                if (_audioTestTabView != null)
                {
                    await _audioTestTabView.InitializeAudioDevicesAsync();
                }

                // Load saved tab selection
                LoadSavedTabSelection();

                UpdateStatus("Ready");
                Logger.Info("Application loaded successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during form load");
                
                // Show error message to user
                MessageBox.Show(
                    $"Error loading application: {ex.Message}\n\nPlease check the log files for more details.",
                    "Application Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                
                UpdateStatus("Error during startup");
            }
        }

        private async void OnFormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_isDisposing) return;
            
            _isDisposing = true;
            
            try
            {
                Logger.Info("Shutting down application");
                UpdateStatus("Shutting down...");
                
                // Save current settings before shutdown
                SaveWindowSettings();
                SaveCurrentTabIndex();
                SavePlayerComponentSettings();
                
                if (_presenter != null)
                {
                    await _presenter.ShutdownAsync();
                }
                
                // Dispose of tab views
                _generalTabView?.Dispose();
                _audioTestTabView?.Dispose();
                
                Logger.Info("Application shutdown completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during form closing");
                // Don't cancel the close operation due to shutdown errors
            }
        }

        private void SaveCurrentTabIndex()
        {
            try
            {
                if (_tabControl != null)
                {
                    _playerSettings.SaveSelectedTab(_tabControl.SelectedIndex);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving current tab index");
            }
        }

        private void SavePlayerComponentSettings()
        {
            try
            {
                // Save player component settings if available
                if (_generalTabView != null)
                {
                    var playerComponent = _generalTabView.GetPlayerComponent();
                    playerComponent?.SaveCurrentSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving player component settings");
            }
        }

        private void LoadSavedTabSelection()
        {
            try
            {
                var savedTabIndex = _playerSettings.GetPlayerSettingInt(PlayerSettingKeys.SelectedTab);
                if (savedTabIndex >= 0 && savedTabIndex < _tabControl.TabCount)
                {
                    _tabControl.SelectedIndex = savedTabIndex;
                    Logger.Debug($"Restored tab selection to index: {savedTabIndex}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading saved tab selection");
            }
        }

        private void OnFormKeyDown(object? sender, KeyEventArgs e)
        {
            // Global keyboard shortcuts
            try
            {
                if (e.Control && e.KeyCode == Keys.O)
                {
                    // Open file dialog through player tab
                if (_tabControl.SelectedIndex == 0 && _generalTabView != null)
                {
                    // Let the GeneralTabView handle file opening
                    Logger.Debug("Ctrl+O shortcut - delegated to general tab");
                }
                    e.Handled = true;
                }
                else if (_tabControl.SelectedIndex == 0) // General tab shortcuts
                {
                    switch (e.KeyCode)
                    {
                        case Keys.F5:
                        case Keys.F6:
                        case Keys.F7:
                        case Keys.Space:
                        case Keys.Escape:
                            // Let the general tab handle these shortcuts internally
                            Logger.Debug($"Function key {e.KeyCode} - delegated to general tab");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling keyboard shortcut");
            }
        }
        
        #endregion



        #region Event Handlers - Component Events
        
        private void OnTabChanged(object? sender, EventArgs e)
        {
            try
            {
                var selectedTab = _tabControl.SelectedTab?.Text ?? "Unknown";
                Logger.Debug($"Tab changed to: {selectedTab}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling tab change");
            }
        }

        private void OnTabStatusChanged(object? sender, string status)
        {
            UpdateStatus(status);
        }

        private void OnLoadingStateChanged(object? sender, bool isLoading)
        {
            _progressBar.Visible = isLoading;
            
            // Disable/enable UI during loading
            _tabControl.Enabled = !isLoading;
        }
        
        #endregion

        #region Helper Methods
        
        private void UpdateStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = message;
                Logger.Debug($"Status updated: {message}");
            }
        }



        /// <summary>
        /// Gets the currently active tab view for external access if needed
        /// </summary>
        public UserControl? GetActiveTabView()
        {
            return _tabControl.SelectedIndex switch
            {
                0 => _generalTabView,
                1 => _audioTestTabView,
                _ => null
            };
        }

        /// <summary>
        /// Switches to a specific tab by index
        /// </summary>
        public void SwitchToTab(int tabIndex)
        {
            if (tabIndex >= 0 && tabIndex < _tabControl.TabCount)
            {
                _tabControl.SelectedIndex = tabIndex;
            }
        }

        /// <summary>
        /// Checks if the form is fully initialized and ready for use
        /// </summary>
        public bool IsFullyInitialized => _isInitialized && !_isDisposing;
        
        #endregion


    }
}