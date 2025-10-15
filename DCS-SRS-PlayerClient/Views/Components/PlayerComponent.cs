using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Controls;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Extensions;
using ShalevOhad.DCS.SRS.Recorder.Core.Settings;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using ShalevOhad.DCS.SRS.Recorder.Core.Helpers;
using ShalevOhad.DCS.SRS.Recorder.Core;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Components
{
    /// <summary>
    /// Component for audio playback functionality including controls, waveform, and frequency filtering
    /// </summary>
    public partial class PlayerComponent : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Services
        
        private IAudioPlaybackService? _audioPlaybackService;
        private IWaveformService? _waveformService;
        private IUIService? _uiService;
        private PlayerSettingsStore _playerSettings;
        
        #endregion

        #region Controls
        
        private Panel _mainPanel;
        private Panel _middleContentPanel;
        private Panel _waveformPanel;
        private Panel _frequencyPanel;
        private Panel _bottomInfoPanel;
        private Panel _enhancedFeaturesPanel;
        
        // Custom controls
        private FrequencyFilterControl _frequencyFilterControl;
        private WaveformSeekBar _waveformSeekBar;
        private VolumeControl _volumeControl;
        private RecentFilesComponent _recentFilesComponent;
        private LiveAnalysisComponent _liveAnalysisComponent;
        
        // Playbook controls
        private Button _playButton;
        private Button _pauseButton;
        private Button _stopButton;
        private Button _bookmarkButton;
        private Button _showEnhancedFeaturesButton;
        
        // Current packet info
        private RichTextBox _currentPacketInfoTextBox;
        
        // Enhanced features
        private bool _showEnhancedFeatures = false;
        
        #endregion

        #region State
        
        private string? _currentFilePath;
        private List<FrequencyModulationInfo> _availableFrequencies = new();
        
        #endregion

        #region Events
        
        public event EventHandler<string>? StatusChanged;
        
        #endregion

        public PlayerComponent()
        {
            InitializeComponent();
            _playerSettings = PlayerSettingsStore.Instance;
            CreateControls();
            SetupEventHandlers();
            LoadPersistedSettings();
            
            // Enable keyboard input
            this.SetStyle(ControlStyles.Selectable, true);
            this.TabStop = true;

            // Handle resize events to ensure proper layout
            this.Resize += OnComponentResize;
        }

        public void Initialize(IAudioPlaybackService audioPlaybackService, IWaveformService waveformService, IUIService uiService)
        {
            _audioPlaybackService = audioPlaybackService ?? throw new ArgumentNullException(nameof(audioPlaybackService));
            _waveformService = waveformService ?? throw new ArgumentNullException(nameof(waveformService));
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));

            // Subscribe to packet started events to update current packet info
            if (_audioPlaybackService != null)
            {
                _audioPlaybackService.PacketStarted += OnPacketStarted;
            }
        }

        private void OnPacketStarted(object? sender, Core.AudioPacketMetadata packet)
        {
            // Update packet info on UI thread
            if (_currentPacketInfoTextBox?.InvokeRequired == true)
            {
                _currentPacketInfoTextBox.Invoke(() => UpdateCurrentPacketInfo(packet));
            }
            else
            {
                UpdateCurrentPacketInfo(packet);
            }
        }

        public async Task LoadFileAsync(string filePath, RecordingFileInfo recordingInfo)
        {
            try
            {
                _currentFilePath = filePath;
                _availableFrequencies = recordingInfo.FrequencyModulations;

                // Set available frequencies in the frequency filter control
                _frequencyFilterControl?.SetAvailableFrequencies(recordingInfo.FrequencyModulations);
                
                // Enable filtering based on user preference
                if (_frequencyFilterControl != null)
                {
                    _frequencyFilterControl.IsFilterEnabled = _playerSettings.GetPlayerSettingBool(PlayerSettingKeys.EnableFrequencyFilterByDefault);
                }

                // Save last loaded file
                _playerSettings.SaveLastRecordingFile(filePath);

                // Load waveform - the WaveformSeekBar will handle loading and timing display internally
                await _waveformService?.LoadWaveformAsync(filePath)!;

                // Also load waveform data directly into the seek bar for visualization
                await _waveformSeekBar?.SetWaveformDataAsync(filePath)!;

                // Enable controls
                _playButton.Enabled = true;
                
                OnStatusChanged($"Player ready: {recordingInfo.FormattedInfo}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading file in player component");
                _uiService?.ShowError($"Error loading file: {ex.Message}");
            }
        }

        public void UpdatePlaybackState(PlaybackState state)
        {
            // Update the waveform seek bar position - it will handle timing display internally
            if (!_waveformService?.IsUserSeeking == true)
            {
                if (_waveformSeekBar != null)
                {
                    // Update position as percentage of maximum
                    var newPosition = (int)(state.ProgressPercent * _waveformSeekBar.Maximum / 100);
                    _waveformSeekBar.Position = newPosition;
                }
            }
        }

        public void UpdateCurrentPacketInfo(Core.AudioPacketMetadata? currentPacket)
        {
            if (_currentPacketInfoTextBox == null) return;

            if (currentPacket == null)
            {
                _currentPacketInfoTextBox.Text = "No packet currently playing";
                return;
            }

            var info = new System.Text.StringBuilder();
            info.AppendLine($"Frequency: {currentPacket.Frequency:F1} MHz ({GetModulationName((int)currentPacket.Modulation)})");
            info.AppendLine($"Player: {currentPacket.PlayerData?.GetDisplayName() ?? "Unknown"}");
            info.AppendLine($"Coalition: {GetCoalitionName(currentPacket.Coalition.ToString())}");
            info.AppendLine($"Time: {currentPacket.Timestamp:HH:mm:ss.fff}");
            info.AppendLine($"Packet ID: {currentPacket.PacketId}");
            
            if (currentPacket.PlayerData?.AircraftInfo?.UnitId > 0)
                info.AppendLine($"Unit ID: {currentPacket.PlayerData.AircraftInfo.UnitId}");
                
            if (!string.IsNullOrEmpty(currentPacket.PlayerData?.AircraftInfo?.UnitType))
                info.AppendLine($"Aircraft: {currentPacket.PlayerData.AircraftInfo.UnitType}");

            _currentPacketInfoTextBox.Text = info.ToString();
            
            // Update live analysis if enabled
            UpdateLiveAnalysis(currentPacket);
        }

        public void AddBookmark(string description = "")
        {
            if (!string.IsNullOrEmpty(_currentFilePath) && _audioPlaybackService != null)
            {
                var position = _audioPlaybackService.CurrentPosition;
                var bookmark = new AudioBookmark(_currentFilePath, position, 
                    string.IsNullOrEmpty(description) ? $"Bookmark at {FormatTime(position)}" : description, 
                    DateTime.Now);
                
                _recentFilesComponent?.AddBookmark(bookmark);
                OnStatusChanged($"Bookmark added at {FormatTime(position)}");
            }
        }

        private void UpdateLiveAnalysis(Core.AudioPacketMetadata packet)
        {
            if (_liveAnalysisComponent?.IsAnalysisEnabled == true)
            {
                // This would integrate with a live analysis service
                // For now, we'll create a placeholder update
                var stats = new LiveAnalysisStats(
                    1, // ProcessedPackets - would be accumulated
                    new Dictionary<double, int> { { packet.Frequency, 1 } },
                    new Dictionary<string, int> { { packet.PlayerData?.GetDisplayName() ?? "Unknown", 1 } },
                    new Dictionary<string, int> { { GetModulationName((int)packet.Modulation), 1 } },
                    TimeSpan.FromSeconds(1),
                    1.0
                );
                
                _liveAnalysisComponent.CurrentStats = stats;
            }
        }

        #region Control Creation

        private void CreateControls()
        {
            // Set background from design language
            this.BackColor = DesignLanguage.Colors.BackgroundDrawing;
            
            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new System.Windows.Forms.Padding(DesignLanguage.LayoutPadding.Dialog),
                BackColor = DesignLanguage.Colors.BackgroundDrawing
            };
            Controls.Add(_mainPanel);

            // Create controls in proper docking order
            CreateEnhancedFeaturesPanel(); // DockStyle.Right - add first (initially hidden)
            CreateBottomInfoPanel();       // DockStyle.Bottom - add second
            CreateMiddleContentPanel();    // DockStyle.Fill - add last
        }

        private void CreateMiddleContentPanel()
        {
            _middleContentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new System.Windows.Forms.Padding(0, 0, 0, DesignLanguage.Sizes.Spacing * 1 + 4),  // Remove top padding
                BackColor = DesignLanguage.Colors.BackgroundDrawing,
                MinimumSize = new Size(600, 300)  // Ensure minimum size for the entire middle section
            };

            // Create frequency panel (on the right)
            CreateFrequencyPanel();

            // Create modern splitter (invisible for smooth appearance)
            var splitter = new Splitter
            {
                Dock = DockStyle.Right,
                Width = 8,
                BackColor = DesignLanguage.Colors.BackgroundDrawing,
                Cursor = Cursors.VSplit
            };

            // Create waveform panel (fills remaining space on the left)
            CreateWaveformPanel();

            // Add controls in proper docking order
            _middleContentPanel.Controls.Add(_frequencyPanel); // DockStyle.Right - add first
            _middleContentPanel.Controls.Add(splitter);        // DockStyle.Right - add second
            _middleContentPanel.Controls.Add(_waveformPanel);  // DockStyle.Fill - add last

            _mainPanel.Controls.Add(_middleContentPanel);
        }

        private void CreateWaveformPanel()
        {
            _waveformPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DesignLanguage.Colors.PanelDrawing,
                Padding = new System.Windows.Forms.Padding(DesignLanguage.LayoutPadding.Default),
                Margin = new Padding(0),
                MinimumSize = new Size(300, 200)  // Ensure minimum size for waveform display
            };
            
            // Add rounded corners using custom paint
            _waveformPanel.Paint += (sender, e) => {
                var rect = new Rectangle(0, 0, _waveformPanel.Width, _waveformPanel.Height);
                Helpers.DrawRoundedPanel(e.Graphics, rect, 12, Color.FromArgb(230, 245, 255));
            };

            // Waveform seek bar with integrated timing display - no separate time header needed
            _waveformSeekBar = new WaveformSeekBar
            {
                Dock = DockStyle.Fill,
                BackColor = DesignLanguage.Colors.PanelDrawing,
                ShowTimeLabels = true,        // Show start/end times at corners
                ShowCurrentTimeLabel = true,  // Show current time near position line
                TimeLabelColor = DesignLanguage.Colors.TextSecondaryDrawing,
                CurrentTimeLabelColor = DesignLanguage.Colors.ErrorDrawing
            };

            _waveformPanel.Controls.Add(_waveformSeekBar);
        }
        
        // Remove the separate time header - timing is now handled by WaveformSeekBar internally
        private void CreateFrequencyPanel()
        {
            _frequencyPanel = new Panel
            {
                Dock = DockStyle.Right,
                BackColor = DesignLanguage.Colors.PanelDrawing,
                Padding = new System.Windows.Forms.Padding(DesignLanguage.LayoutPadding.Default),
                Margin = new Padding(0),
                MinimumSize = new Size(200, 0),  // Minimum width of 200px
                MaximumSize = new Size(500, 0)   // Maximum width of 500px
            };
            
            // Add rounded corners using custom paint
            _frequencyPanel.Paint += (sender, e) => {
                var rect = new Rectangle(0, 0, _frequencyPanel.Width, _frequencyPanel.Height);
                Helpers.DrawRoundedPanel(e.Graphics, rect, 12, Color.FromArgb(230, 245, 255));
            };

            // Create and configure the frequency filter control
            _frequencyFilterControl = new FrequencyFilterControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            // Subscribe to frequency filter events
            _frequencyFilterControl.SelectedFrequenciesChanged += OnFrequencyFilterChanged;

            _frequencyPanel.Controls.Add(_frequencyFilterControl);
        }

        private void CreateBottomInfoPanel()
        {
            _bottomInfoPanel = new Panel
            {
                Height = 140,  // Increased height for better packet info display
                Dock = DockStyle.Bottom,
                BackColor = DesignLanguage.Colors.PanelAltDrawing,
                Padding = new System.Windows.Forms.Padding(DesignLanguage.LayoutPadding.Default),
                Margin = new Padding(0)
            };
            
            // Add rounded corners using custom paint
            _bottomInfoPanel.Paint += (sender, e) => {
                var rect = new Rectangle(0, 0, _bottomInfoPanel.Width, _bottomInfoPanel.Height);
                Helpers.DrawRoundedPanel(e.Graphics, rect, 12, Color.FromArgb(220, 240, 255));
            };

            // Create enhanced playback controls panel on the left
            var playbackControlsPanel = CreateEnhancedPlaybackPanel();

            // Create packet info panel on the right
            var packetInfoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(12, 0, 0, 0)
            };

            var infoLabel = new Label
            {
                Text = "Current Packet Info",
                Height = 24,
                Dock = DockStyle.Top,
                Font = DesignLanguage.Fonts.GetDrawingFont(10f, FontStyle.Bold),
                ForeColor = DesignLanguage.Colors.TextPrimaryDrawing,
                BackColor = Color.Transparent
            };

            _currentPacketInfoTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = DesignLanguage.Colors.PanelDrawing,
                ForeColor = DesignLanguage.Colors.TextPrimaryDrawing,
                BorderStyle = BorderStyle.FixedSingle,
                Font = DesignLanguage.Fonts.GetDrawingFont(9f),
                Text = "No packet currently playing",
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Margin = new System.Windows.Forms.Padding(0, DesignLanguage.Sizes.Spacing / 2, 0, 0)
            };

            packetInfoPanel.Controls.Add(_currentPacketInfoTextBox);
            packetInfoPanel.Controls.Add(infoLabel);

            _bottomInfoPanel.Controls.Add(playbackControlsPanel);
            _bottomInfoPanel.Controls.Add(packetInfoPanel);

            _mainPanel.Controls.Add(_bottomInfoPanel);
        }

        private void CreateEnhancedFeaturesPanel()
        {
            _enhancedFeaturesPanel = new Panel
            {
                Width = 400,
                Dock = DockStyle.Right,
                BackColor = DesignLanguage.Colors.PanelAltDrawing,
                Padding = new System.Windows.Forms.Padding(DesignLanguage.LayoutPadding.Default),
                Visible = _showEnhancedFeatures
            };

            // Add rounded corners
            _enhancedFeaturesPanel.Paint += (sender, e) => 
                Helpers.DrawRoundedPanel(e.Graphics, _enhancedFeaturesPanel.ClientRectangle, 12, Color.FromArgb(235, 245, 255));

            // Create tab control for enhanced features
            var enhancedTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = DesignLanguage.Fonts.GetDrawingFont(9f),
                Appearance = TabAppearance.Normal
            };

            // Recent Files & Bookmarks Tab
            var filesTab = new TabPage("Files & Bookmarks")
            {
                BackColor = Color.FromArgb(250, 252, 255),
                UseVisualStyleBackColor = true
            };

            _recentFilesComponent = new RecentFilesComponent
            {
                Dock = DockStyle.Fill
            };
            _recentFilesComponent.FileSelected += OnEnhancedFileSelected;
            _recentFilesComponent.BookmarkSelected += OnBookmarkSelected;
            _recentFilesComponent.StatusChanged += (s, status) => OnStatusChanged(status);

            filesTab.Controls.Add(_recentFilesComponent);

            // Live Analysis Tab
            var analysisTab = new TabPage("Live Analysis")
            {
                BackColor = Color.FromArgb(250, 252, 255),
                UseVisualStyleBackColor = true
            };

            _liveAnalysisComponent = new LiveAnalysisComponent
            {
                Dock = DockStyle.Fill
            };
            _liveAnalysisComponent.StatusChanged += (s, status) => OnStatusChanged(status);

            analysisTab.Controls.Add(_liveAnalysisComponent);

            enhancedTabControl.TabPages.AddRange(new[] { filesTab, analysisTab });
            _enhancedFeaturesPanel.Controls.Add(enhancedTabControl);

            _mainPanel.Controls.Add(_enhancedFeaturesPanel);
        }

        private Panel CreateEnhancedPlaybackPanel()
        {
            var playbackPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 180,  // Slightly wider for bookmark button
                BackColor = Color.Transparent,
                Padding = new System.Windows.Forms.Padding(DesignLanguage.LayoutPadding.Default)
            };

            // Create vertical layout for controls
            var controlsContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            // Create enhanced playback buttons with bookmark
            var buttonsPanel = CreateEnhancedPlaybackButtons();
            buttonsPanel.Dock = DockStyle.Top;
            buttonsPanel.Height = 70 // Increased height for two rows
            ;

            // Create compact volume control
            _volumeControl = new VolumeControl
            {
                Dock = DockStyle.Bottom,
                Height = 35,
                BackColor = DesignLanguage.Colors.PanelDrawing  // Use design language background
            };

            controlsContainer.Controls.Add(buttonsPanel);
            controlsContainer.Controls.Add(_volumeControl);
            playbackPanel.Controls.Add(controlsContainer);

            return playbackPanel;
        }

        private Panel CreateEnhancedPlaybackButtons()
        {
            var panel = new Panel
            {
                BackColor = Color.Transparent,
                Height = 70
            };

            // Create tooltip for buttons
            var tooltip = new ToolTip
            {
                AutoPopDelay = 3000,
                InitialDelay = 1000,
                ReshowDelay = 500,
                ShowAlways = true
            };

            // First row - main playback controls (text labels instead of icons)
            _playButton = CreateCompactPlaybackButton("Play", DesignLanguage.Colors.PrimaryDrawing);
            _pauseButton = CreateCompactPlaybackButton("Pause", DesignLanguage.Colors.WarningDrawing);
            _stopButton = CreateCompactPlaybackButton("Stop", DesignLanguage.Colors.ErrorDrawing);

            _playButton.Enabled = false;
            _pauseButton.Enabled = false;
            _stopButton.Enabled = false;

            // Position first row buttons
            _playButton.Location = new Point(8, 8);
            _pauseButton.Location = new Point(8 + _playButton.Width + 8, 8);
            _stopButton.Location = new Point(8 + (_playButton.Width + 8) * 2, 8);

            // Second row - enhanced features (use text labels)
            _bookmarkButton = CreateCompactPlaybackButton("Bookmark", DesignLanguage.Colors.AccentDrawing);
            _showEnhancedFeaturesButton = CreateCompactPlaybackButton("More", DesignLanguage.Colors.AccentDrawing);

            _bookmarkButton.Location = new Point(8, 42);
            _showEnhancedFeaturesButton.Location = new Point(8 + _bookmarkButton.Width + 8, 42);

            tooltip.SetToolTip(_playButton, "Start playback (Spacebar)");
            tooltip.SetToolTip(_pauseButton, "Pause/Resume playback (Spacebar)");
            tooltip.SetToolTip(_stopButton, "Stop playback (Escape)");
            tooltip.SetToolTip(_bookmarkButton, "Add bookmark at current position (Ctrl+B)");
            tooltip.SetToolTip(_showEnhancedFeaturesButton, "Show/Hide enhanced features panel");

            // Wire up new button events
            _bookmarkButton.Click += OnBookmarkButtonClick;
            _showEnhancedFeaturesButton.Click += OnShowEnhancedFeaturesClick;

            panel.Controls.AddRange(new Control[] 
            { 
                _playButton, _pauseButton, _stopButton, 
                _bookmarkButton, _showEnhancedFeaturesButton 
            });
            
            return panel;
        }

        private void OnEnhancedFileSelected(object? sender, string filePath)
        {
            // This would trigger file loading through the parent component
            OnStatusChanged($"Selected file: {System.IO.Path.GetFileName(filePath)}");
            
            // Fire an event or callback to the parent to handle file loading
            // For now, we'll just show a status message
        }

        private void OnBookmarkSelected(object? sender, AudioBookmark bookmark)
        {
            // Seek to bookmark position if the same file is loaded
            if (_currentFilePath == bookmark.FilePath && _audioPlaybackService != null)
            {
                try
                {
                    _audioPlaybackService.SeekToAsync(bookmark.Position);
                    OnStatusChanged($"Jumped to bookmark: {bookmark.Description}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error seeking to bookmark");
                    _uiService?.ShowError($"Error seeking to bookmark: {ex.Message}");
                }
            }
            else
            {
                OnStatusChanged($"Bookmark is for different file: {System.IO.Path.GetFileName(bookmark.FilePath)}");
            }
        }

        private void OnBookmarkButtonClick(object? sender, EventArgs e)
        {
            // Simple approach - use a default description for now
            // In a full implementation, you could create a custom dialog
            var defaultDescription = $"Bookmark at {DateTime.Now:HH:mm:ss}";
            AddBookmark(defaultDescription);
        }

        private void OnShowEnhancedFeaturesClick(object? sender, EventArgs e)
        {
            _showEnhancedFeatures = !_showEnhancedFeatures;
            _enhancedFeaturesPanel.Visible = _showEnhancedFeatures;
            
            // Update button appearance
            _showEnhancedFeaturesButton.BackColor = _showEnhancedFeatures 
                ? Color.FromArgb(120, 80, 200) 
                : Color.FromArgb(150, 100, 255);
                
            OnStatusChanged(_showEnhancedFeatures ? "Enhanced features shown" : "Enhanced features hidden");
        }

        private Button CreateCompactPlaybackButton(string symbol, Color accentColor)
        {
            var button = new Button
            {
                Text = symbol,
                Size = new Size(80, 32),  // Slightly larger size for better text visibility
                Font = DesignLanguage.Fonts.GetDrawingFont(DesignLanguage.Fonts.Normal, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = accentColor,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = ControlPaint.Dark(accentColor, 0.3f);
            
            // Simple hover effects without custom painting
            var hoverColor = ControlPaint.Light(accentColor, 0.2f);
            var pressedColor = ControlPaint.Dark(accentColor, 0.2f);
            var disabledColor = Color.FromArgb(80, 80, 90);
            
            button.MouseEnter += (s, e) => {
                if (button.Enabled)
                    button.BackColor = hoverColor;
            };
            button.MouseLeave += (s, e) => {
                button.BackColor = accentColor;
            };
            button.MouseDown += (s, e) => {
                button.BackColor = pressedColor;
            };
            button.MouseUp += (s, e) => {
                if (button.Enabled)
                    button.BackColor = button.ClientRectangle.Contains(button.PointToClient(Cursor.Position)) ? hoverColor : accentColor;
                    button.BackColor = hoverColor;
            };

            // Add rounded appearance
            button.Paint += (sender, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // Draw rounded rectangle background
                var rect = new Rectangle(1, 1, button.Width - 2, button.Height - 2);
                using (var path = Helpers.CreateRoundedRectanglePath(rect, DesignLanguage.Sizes.SmallCornerRadius))
                using (var brush = new SolidBrush(button.BackColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
                
                // Draw text label centered
                var textSize = e.Graphics.MeasureString(button.Text, button.Font);
                var textRect = new PointF(
                    (button.Width - textSize.Width) / 2,
                    (button.Height - textSize.Height) / 2);
                using (var textBrush = new SolidBrush(button.ForeColor))
                {
                    e.Graphics.DrawString(button.Text, button.Font, textBrush, textRect);
                }
            };
            
            return button;
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            
            Name = "PlayerComponent";
            Size = new Size(600, 400);
            MinimumSize = new Size(700, 500);  // Set minimum size for the component
            
            ResumeLayout(false);
        }

        private void OnComponentResize(object? sender, EventArgs e)
        {
            // Ensure proper layout when component is resized
            if (_mainPanel != null)
            {
                _mainPanel.PerformLayout();
            }
            
            // Ensure waveform panel maintains minimum size
            if (_waveformPanel != null && _waveformPanel.Width < 300)
            {
                // If waveform panel gets too small, adjust frequency panel size
                if (_frequencyPanel != null && _middleContentPanel != null)
                {
                    int availableWidth = _middleContentPanel.ClientSize.Width;
                    int minWaveformWidth = 300;
                    int maxFrequencyWidth = Math.Min(500, availableWidth - minWaveformWidth - 8); // 8 for splitter
                    
                    if (_frequencyPanel.Width > maxFrequencyWidth)
                    {
                        _frequencyPanel.Width = Math.Max(200, maxFrequencyWidth);
                    }
                }
            }
        }

        #region Event Handlers

        private void SetupEventHandlers()
        {
            // Playback controls
            _playButton.Click += async (s, e) => await OnPlay();
            _pauseButton.Click += async (s, e) => await OnPause();
            _stopButton.Click += async (s, e) => await OnStop();

            // Component events
            _waveformSeekBar.PositionChanged += OnWaveformPositionChanged;
            _volumeControl.VolumeChanged += OnVolumeChanged;

            // Keyboard shortcuts
            this.KeyDown += OnKeyDown;
        }

        private async void OnKeyDown(object? sender, KeyEventArgs e)
        {
            try
            {
                if (e.Control && e.KeyCode == Keys.B)
                {
                    // Ctrl+B: Add bookmark
                    e.Handled = true;
                    OnBookmarkButtonClick(this, EventArgs.Empty);
                }
                else if (e.Control && e.KeyCode == Keys.E)
                {
                    // Ctrl+E: Toggle enhanced features
                    e.Handled = true;
                    OnShowEnhancedFeaturesClick(this, EventArgs.Empty);
                }
                else
                {
                    switch (e.KeyCode)
                    {
                        case Keys.Space:
                            e.Handled = true;
                            if (_audioPlaybackService?.IsPlaying == true)
                            {
                                await OnPause();
                            }
                            else if (_playButton.Enabled)
                            {
                                await OnPlay();
                            }
                            break;
                        case Keys.Escape:
                            if (_stopButton.Enabled)
                            {
                                e.Handled = true;
                                await OnStop();
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling keyboard shortcut");
            }
        }

        private async Task OnPlay()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                _uiService?.ShowWarning("Please open a recording file first.");
                return;
            }

            try
            {
                // Get selected frequencies from tree view
                var selectedFrequencies = GetSelectedFrequencies();
                var filterConfig = new FrequencyFilterConfig(
                    selectedFrequencies.Count > 0,
                    selectedFrequencies
                );
                
                var audioConfig = new AudioConfig(_volumeControl.CurrentVolume);

                await _audioPlaybackService?.StartAsync(_currentFilePath, filterConfig, audioConfig)!;
                
                _playButton.Enabled = false;
                _pauseButton.Enabled = true;
                _stopButton.Enabled = true;
                _pauseButton.Text = "Pause";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting playback");
                _uiService?.ShowError($"Error starting playback: {ex.Message}");
            }
        }

        private List<FrequencyModulationInfo> GetSelectedFrequencies()
        {
            return _frequencyFilterControl?.SelectedFrequencies ?? new List<FrequencyModulationInfo>();
        }

        private async Task OnPause()
        {
            try
            {
                if (_audioPlaybackService?.IsPaused == true)
                {
                    await _audioPlaybackService.ResumeAsync();
                    _pauseButton.Text = "Pause";
                }
                else
                {
                    await _audioPlaybackService?.PauseAsync()!;
                    _pauseButton.Text = "Resume";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error pausing/resuming playback");
                _uiService?.ShowError($"Error with pause/resume: {ex.Message}");
            }
        }

        private async Task OnStop()
        {
            try
            {
                await _audioPlaybackService?.StopAsync()!;
                
                _playButton.Enabled = true;
                _pauseButton.Enabled = false;
                _stopButton.Enabled = false;
                _pauseButton.Text = "Pause";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping playback");
                _uiService?.ShowError($"Error stopping playback: {ex.Message}");
            }
        }

        private async void OnWaveformPositionChanged(object? sender, int position)
        {
            if (_audioPlaybackService?.IsPlaying == true)
            {
                try
                {
                    var duration = _audioPlaybackService.TotalDuration;
                    var newPosition = TimeSpan.FromMilliseconds(position * duration.TotalMilliseconds / 100);
                    await _audioPlaybackService.SeekToAsync(newPosition);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error seeking to position");
                }
            }
        }

        private async void OnVolumeChanged(object? sender, float volume)
        {
            try
            {
                if (_audioPlaybackService != null)
                {
                    await _audioPlaybackService.SetVolumeAsync(volume);
                }
                
                // Save volume setting (convert to 0-200 range)
                _playerSettings.SetPlayerSetting(PlayerSettingKeys.MasterVolume, (int)(volume * 100));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error changing volume");
            }
        }

        private void OnFrequencyFilterChanged(object? sender, List<FrequencyModulationInfo> selectedFrequencies)
        {
            try
            {
                UpdateFrequencyFilter();
                
                // Save frequency filter enabled state
                var isEnabled = _frequencyFilterControl?.IsFilterEnabled ?? false;
                _playerSettings.SetPlayerSetting(PlayerSettingKeys.EnableFrequencyFilterByDefault, isEnabled);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling frequency filter change");
            }
        }

        #endregion

        #region Settings Management

        private void LoadPersistedSettings()
        {
            try
            {
                // Load volume setting (convert from 0-200 range to 0-2.0 range)
                var savedVolume = _playerSettings.GetDefaultMasterVolume();
                if (_volumeControl != null)
                {
                    _volumeControl.Volume = savedVolume / 100.0f;
                }

                // Apply persisted player theme (if any). The Helpers.ApplyThemeFile only applies in-memory;
                // persist via PlayerSettingsStore so the player keeps its own theme.
                try
                {
                    var themeFile = _playerSettings.GetPlayerSettingString(PlayerSettingKeys.ThemeFile);
                    if (!string.IsNullOrEmpty(themeFile))
                    {
                        var applied = Helpers.ApplyThemeFile(themeFile);
                        if (!applied)
                        {
                            Logger.Warn($"Failed to apply player theme '{themeFile}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Unable to apply persisted player theme");
                }
                
                Logger.Debug($"Loaded persisted settings - Volume: {savedVolume}%");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading persisted settings");
            }
        }

        /// <summary>
        /// Get the last recording file that was loaded
        /// </summary>
        public string GetLastRecordingFile()
        {
            return _playerSettings.GetPlayerSettingString(PlayerSettingKeys.LastRecordingFile);
        }

        /// <summary>
        /// Save current player state settings
        /// </summary>
        public void SaveCurrentSettings()
        {
            try
            {
                // Save current volume
                if (_volumeControl != null)
                {
                    _playerSettings.SetPlayerSetting(PlayerSettingKeys.MasterVolume, 
                        (int)(_volumeControl.Volume * 100));
                }

                // Save current frequency filter state
                if (_frequencyFilterControl != null)
                {
                    _playerSettings.SetPlayerSetting(PlayerSettingKeys.EnableFrequencyFilterByDefault,
                        _frequencyFilterControl.IsFilterEnabled);
                }

                Logger.Debug("Saved current player settings");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving current settings");
            }
        }

        /// <summary>
        /// Apply a theme and persist it to the player settings store.
        /// The UI delegate that knows it's operating in the Player context should call this.
        /// </summary>
        /// <param name="fileName">Theme filename located in the themes folder</param>
        public void ApplyAndSavePlayerTheme(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName)) return;

                var applied = Helpers.ApplyThemeFile(fileName);
                if (applied)
                {
                    _playerSettings.SetPlayerSetting(PlayerSettingKeys.ThemeFile, fileName);
                    OnStatusChanged($"Theme applied: {fileName}");
                }
                else
                {
                    _uiService?.ShowError($"Failed to apply theme: {fileName}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error applying/saving player theme");
                _uiService?.ShowError($"Error applying theme: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private async void UpdateFrequencyFilter()
        {
            try
            {
                // Get the current filter configuration from the frequency filter control
                var filterConfig = _frequencyFilterControl?.GetCurrentFilter();
                if (filterConfig == null) return;

                var selectedFrequencies = filterConfig.SelectedFrequencies;

                // Create frequency filter configuration for waveform
                var selectedFreqMods = selectedFrequencies
                    .Select(f => (f.Frequency, f.Modulation))
                    .ToList();

                // Apply to waveform display
                _waveformSeekBar?.SetFrequencyFilter(selectedFreqMods, filterConfig.IsEnabled && selectedFreqMods.Count > 0);

                // Apply to audio playback if playing
                if (_audioPlaybackService?.IsPlaying == true || _audioPlaybackService?.IsPaused == true)
                {
                    await _audioPlaybackService.SetFrequencyFilterAsync(filterConfig);
                }

                // Save frequency filter enabled state
                _playerSettings.SetPlayerSetting(PlayerSettingKeys.EnableFrequencyFilterByDefault, filterConfig.IsEnabled);

                var statusMessage = filterConfig.IsEnabled 
                    ? $"Filter enabled: {selectedFrequencies.Count} frequencies selected"
                    : "Filter disabled";
                    
                OnStatusChanged(statusMessage);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating frequency filter");
                _uiService?.ShowError($"Error updating filter: {ex.Message}");
            }
        }

        private static string GetCoalitionName(string coalition)
        {
            return coalition switch
            {
                "1" => "Red",
                "2" => "Blue",
                _ => "Neutral"
            };
        }

        private static string GetModulationName(int modulation)
        {
            return ((Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player.Modulation)modulation).ToString();
        }

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

        #endregion
    }
#endregion
}
