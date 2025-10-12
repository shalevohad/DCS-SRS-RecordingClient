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
        private Panel _topControlsPanel;
        private Panel _middleContentPanel;
        private Panel _waveformPanel;
        private Panel _frequencyPanel;
        private Panel _bottomInfoPanel;
        
        // Custom controls
        private FrequencyFilterControl _frequencyFilterControl;
        private WaveformSeekBar _waveformSeekBar;
        private VolumeControl _volumeControl;
        
        // Playbook controls
        private Button _playButton;
        private Button _pauseButton;
        private Button _stopButton;
        
        // Current packet info
        private RichTextBox _currentPacketInfoTextBox;
        
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
        }

        #region Control Creation

        private void CreateControls()
        {
            // Set light blue background for the entire component
            this.BackColor = Color.FromArgb(240, 248, 255);
            
            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 12, 16, 12),
                BackColor = Color.FromArgb(240, 248, 255)
            };
            Controls.Add(_mainPanel);

            // Create controls in proper docking order
            // Controls with DockStyle.Fill should be added last
            CreateBottomInfoPanel();    // DockStyle.Bottom - add first
            CreateMiddleContentPanel(); // DockStyle.Fill - add last (no top panel needed)
        }

        private void CreateTopControlsPanel()
        {
            // Remove the top controls panel entirely - timing will be handled by the waveform
            // This method is kept for compatibility but does nothing
        }

        private void CreateMiddleContentPanel()
        {
            _middleContentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 6, 0, 12),  // Reduced top padding since no top panel
                BackColor = Color.FromArgb(240, 248, 255)
            };

            // Create frequency panel (on the right)
            CreateFrequencyPanel();

            // Create modern splitter (invisible for smooth appearance)
            var splitter = new Splitter
            {
                Dock = DockStyle.Right,
                Width = 8,
                BackColor = Color.FromArgb(240, 248, 255),
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
                BackColor = Color.FromArgb(230, 245, 255),
                Padding = new Padding(16, 12, 8, 12),
                Margin = new Padding(0, 0, 0, 0)
            };
            
            // Add rounded corners using custom paint
            _waveformPanel.Paint += (sender, e) => {
                var rect = new Rectangle(0, 0, _waveformPanel.Width, _waveformPanel.Height);
                using (var path = CreateRoundedRectanglePath(rect, 12))
                using (var brush = new SolidBrush(Color.FromArgb(230, 245, 255)))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(brush, path);
                }
            };

            // Waveform seek bar with integrated timing display - no separate time header needed
            _waveformSeekBar = new WaveformSeekBar
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(230, 245, 255),
                ShowTimeLabels = true,        // Show start/end times at corners
                ShowCurrentTimeLabel = true,  // Show current time near position line
                TimeLabelColor = Color.FromArgb(60, 80, 120),
                CurrentTimeLabelColor = Color.FromArgb(180, 20, 20)
            };

            _waveformPanel.Controls.Add(_waveformSeekBar);
        }
        
        // Remove the separate time header - timing is now handled by WaveformSeekBar internally

        private void CreateFrequencyPanel()
        {
            _frequencyPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 350,
                BackColor = Color.FromArgb(230, 245, 255),
                Padding = new Padding(8, 4, 8, 8),
                Margin = new Padding(0, 0, 0, 0)
            };
            
            // Add rounded corners using custom paint
            _frequencyPanel.Paint += (sender, e) => {
                var rect = new Rectangle(0, 0, _frequencyPanel.Width, _frequencyPanel.Height);
                using (var path = CreateRoundedRectanglePath(rect, 12))
                using (var brush = new SolidBrush(Color.FromArgb(230, 245, 255)))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(brush, path);
                }
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
                BackColor = Color.FromArgb(220, 240, 255),
                Padding = new Padding(16, 12, 16, 16),
                Margin = new Padding(0, 0, 0, 0)
            };
            
            // Add rounded corners using custom paint
            _bottomInfoPanel.Paint += (sender, e) => {
                var rect = new Rectangle(0, 0, _bottomInfoPanel.Width, _bottomInfoPanel.Height);
                using (var path = CreateRoundedRectanglePath(rect, 12))
                using (var brush = new SolidBrush(Color.FromArgb(220, 240, 255)))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(brush, path);
                }
            };

            // Create minimalistic playback controls panel on the left
            var playbackControlsPanel = CreateMinimalisticPlaybackPanel();

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
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 80),
                BackColor = Color.Transparent
            };

            _currentPacketInfoTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(245, 250, 255),
                ForeColor = Color.FromArgb(40, 40, 50),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                Text = "No packet currently playing",
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Margin = new Padding(0, 4, 0, 0)
            };

            packetInfoPanel.Controls.Add(_currentPacketInfoTextBox);
            packetInfoPanel.Controls.Add(infoLabel);

            _bottomInfoPanel.Controls.Add(playbackControlsPanel);
            _bottomInfoPanel.Controls.Add(packetInfoPanel);

            _mainPanel.Controls.Add(_bottomInfoPanel);
        }







        private Panel CreateMinimalisticPlaybackPanel()
        {
            var playbackPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 160,  // Compact width
                BackColor = Color.Transparent,
                Padding = new Padding(8)
            };

            // Create vertical layout for controls
            var controlsContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            // Create compact playback buttons
            var buttonsPanel = CreateCompactPlaybackButtons();
            buttonsPanel.Dock = DockStyle.Top;
            buttonsPanel.Height = 45;

            // Create compact volume control
            _volumeControl = new VolumeControl
            {
                Dock = DockStyle.Bottom,
                Height = 35,
                BackColor = Color.Transparent
            };

            controlsContainer.Controls.Add(buttonsPanel);
            controlsContainer.Controls.Add(_volumeControl);
            playbackPanel.Controls.Add(controlsContainer);

            return playbackPanel;
        }

        private Panel CreateCompactPlaybackButtons()
        {
            var panel = new Panel
            {
                BackColor = Color.Transparent,
                Height = 45
            };

            // Create tooltip for buttons
            var tooltip = new ToolTip
            {
                AutoPopDelay = 3000,
                InitialDelay = 1000,
                ReshowDelay = 500,
                ShowAlways = true
            };

            // Create smaller, more compact buttons
            _playButton = CreateCompactPlaybackButton("?", Color.FromArgb(70, 175, 70));
            _pauseButton = CreateCompactPlaybackButton("?", Color.FromArgb(255, 165, 0));
            _stopButton = CreateCompactPlaybackButton("?", Color.FromArgb(220, 70, 70));

            _playButton.Enabled = false;
            _pauseButton.Enabled = false;
            _stopButton.Enabled = false;

            // Position buttons horizontally
            _playButton.Location = new Point(8, 8);
            _pauseButton.Location = new Point(42, 8);
            _stopButton.Location = new Point(76, 8);

            tooltip.SetToolTip(_playButton, "Start playback (Spacebar)");
            tooltip.SetToolTip(_pauseButton, "Pause/Resume playback (Spacebar)");
            tooltip.SetToolTip(_stopButton, "Stop playback (Escape)");

            panel.Controls.AddRange(new Control[] { _playButton, _pauseButton, _stopButton });
            return panel;
        }

        private Button CreateCompactPlaybackButton(string symbol, Color accentColor)
        {
            var button = new Button
            {
                Text = symbol,
                Size = new Size(28, 28),  // Compact size
                Font = new Font("Segoe UI Symbol", 10F, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = accentColor,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            button.FlatAppearance.BorderSize = 0;
            
            // Create hover and pressed colors
            var hoverColor = ControlPaint.Light(accentColor, 0.2f);
            var pressedColor = ControlPaint.Dark(accentColor, 0.2f);
            var disabledColor = Color.FromArgb(80, 80, 90);
            
            button.MouseEnter += (s, e) => {
                if (button.Enabled)
                    button.BackColor = hoverColor;
            };
            
            button.MouseLeave += (s, e) => {
                if (button.Enabled)
                    button.BackColor = accentColor;
            };
            
            button.MouseDown += (s, e) => {
                if (button.Enabled)
                    button.BackColor = pressedColor;
            };
            
            button.MouseUp += (s, e) => {
                if (button.Enabled)
                    button.BackColor = button.ClientRectangle.Contains(button.PointToClient(Cursor.Position)) ? hoverColor : accentColor;
            };
            
            button.EnabledChanged += (s, e) => {
                button.BackColor = button.Enabled ? accentColor : disabledColor;
                button.ForeColor = button.Enabled ? Color.White : Color.FromArgb(120, 120, 120);
            };
            
            // Add rounded appearance
            button.Paint += (sender, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = new Rectangle(1, 1, button.Width - 2, button.Height - 2);
                using (var brush = new SolidBrush(button.BackColor))
                {
                    e.Graphics.FillEllipse(brush, rect);
                }
                
                // Draw symbol
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

        #endregion

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

        private static Color GetCoalitionColor(string coalition)
        {
            return coalition switch
            {
                "Red" => Color.DarkRed,
                "Blue" => Color.DarkBlue,
                _ => Color.DarkGreen
            };
        }

        private static Color GetModernCoalitionColor(string coalition)
        {
            return coalition switch
            {
                "Red" => Color.FromArgb(255, 100, 100),
                "Blue" => Color.FromArgb(100, 150, 255),
                _ => Color.FromArgb(100, 220, 120)
            };
        }

        private static Color GetLightThemeCoalitionColor(string coalition)
        {
            return coalition switch
            {
                "Red" => Color.FromArgb(180, 20, 20),
                "Blue" => Color.FromArgb(20, 80, 180),
                _ => Color.FromArgb(20, 140, 60)
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

        #region Modern UI Helpers

        private Button CreateModernButton(string text, Point location, Size size)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(40, 60, 100),
                BackColor = Color.FromArgb(200, 230, 255),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 220, 255);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(160, 200, 240);
            
            // Add rounded corners and hover effects
            button.Paint += (sender, e) => {
                var rect = new Rectangle(0, 0, button.Width, button.Height);
                using (var path = CreateRoundedRectanglePath(rect, 8))
                using (var brush = new SolidBrush(button.BackColor))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(brush, path);
                    
                    // Add subtle border
                    using (var borderPen = new Pen(Color.FromArgb(150, 180, 220), 1))
                    {
                        e.Graphics.DrawPath(borderPen, path);
                    }
                    
                    // Draw text manually for better control
                    var textSize = e.Graphics.MeasureString(button.Text, button.Font);
                    var textRect = new PointF(
                        (button.Width - textSize.Width) / 2,
                        (button.Height - textSize.Height) / 2);
                    using (var textBrush = new SolidBrush(button.ForeColor))
                    {
                        e.Graphics.DrawString(button.Text, button.Font, textBrush, textRect);
                    }
                }
            };
            
            return button;
        }

        private Button CreateModernPlaybackButton(string symbol, Size size, Color accentColor)
        {
            var button = new Button
            {
                Text = symbol,
                Size = size,
                Font = new Font("Segoe UI Symbol", 14F, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = accentColor,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            button.FlatAppearance.BorderSize = 0;
            
            // Create hover and pressed colors
            var hoverColor = ControlPaint.Light(accentColor, 0.2f);
            var pressedColor = ControlPaint.Dark(accentColor, 0.2f);
            var disabledColor = Color.FromArgb(80, 80, 90);
            
            button.MouseEnter += (s, e) => {
                if (button.Enabled)
                    button.BackColor = hoverColor;
            };
            
            button.MouseLeave += (s, e) => {
                if (button.Enabled)
                    button.BackColor = accentColor;
            };
            
            button.MouseDown += (s, e) => {
                if (button.Enabled)
                    button.BackColor = pressedColor;
            };
            
            button.MouseUp += (s, e) => {
                if (button.Enabled)
                    button.BackColor = button.ClientRectangle.Contains(button.PointToClient(Cursor.Position)) ? hoverColor : accentColor;
            };
            
            button.EnabledChanged += (s, e) => {
                button.BackColor = button.Enabled ? accentColor : disabledColor;
                button.ForeColor = button.Enabled ? Color.White : Color.FromArgb(120, 120, 120);
            };
            
            // Add circular appearance
            button.Paint += (sender, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = new Rectangle(2, 2, button.Width - 4, button.Height - 4);
                using (var brush = new SolidBrush(button.BackColor))
                {
                    e.Graphics.FillEllipse(brush, rect);
                }
                
                // Add subtle shadow
                if (button.Enabled)
                {
                    var shadowRect = new Rectangle(0, 0, button.Width, button.Height);
                    using (var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                    {
                        e.Graphics.FillEllipse(shadowBrush, shadowRect);
                    }
                }
                
                // Draw symbol
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

        private System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int cornerRadius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var diameter = cornerRadius * 2;
            
            // Top left arc
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            // Top right arc
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            // Bottom right arc
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            // Bottom left arc
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            
            path.CloseFigure();
            return path;
        }

        #endregion

        private void InitializeComponent()
        {
            SuspendLayout();
            
            Name = "PlayerComponent";
            Size = new Size(600, 400);
            
            ResumeLayout(false);
        }
    }
}