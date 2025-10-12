using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Controls;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Tabs
{
    /// <summary>
    /// Tab control for audio playback functionality
    /// </summary>
    public partial class PlayerTabView : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Services
        private IRecordingInfoService? _recordingInfoService;
        private IAudioPlaybackService? _audioPlaybackService;
        private ISettingsService? _settingsService;
        private IUIService? _uiService;
        private IWaveformService? _waveformService;

        // Controls
        private Panel _playerMainPanel;
        private Panel _controlsPanel;
        private Panel _waveformPanel;
        private Panel _filtersPanel;
        private Panel _fileSelectionPanel;

        // File selection controls
        private TextBox _filePathTextBox;
        private Button _browseButton;
        private Label _fileSelectionLabel;

        // Custom controls
        private WaveformSeekBar _waveformSeekBar;
        private VolumeControl _volumeControl;
        private FrequencyFilterControl _frequencyFilterControl;

        // Playback controls
        private Button _playButton;
        private Button _pauseButton;
        private Button _stopButton;
        private Label _positionLabel;
        private Label _durationLabel;

        private string? _currentFilePath;
        private bool _suppressFileChangeEvents; // To prevent circular updates

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<string>? FileSelected;

        public PlayerTabView()
        {
            InitializeComponent();
            CreateControls();
            SetupEventHandlers();
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

            SetupServiceEventHandlers();
        }

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
                _filePathTextBox.Text = filePath;

                // Update frequency filter with available frequencies
                _frequencyFilterControl.SetAvailableFrequencies(recordingInfo.FrequencyModulations);

                // Load waveform
                await _waveformService?.LoadWaveformAsync(filePath)!;

                // Update UI
                _durationLabel.Text = FormatTime(recordingInfo.TotalDuration);
                OnStatusChanged($"Loaded: {Path.GetFileName(filePath)} | {recordingInfo.FormattedInfo}");

                // Save as last used file and notify other tabs (unless this is a shared update)
                if (_settingsService != null)
                {
                    _settingsService.LastFilePath = filePath;
                }

                if (!_suppressFileChangeEvents)
                {
                    FileSelected?.Invoke(this, filePath);
                }

                _playButton.Enabled = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading file: {filePath}", filePath);
                _uiService?.ShowError($"Error loading file: {ex.Message}");
            }
        }

        private void CreateControls()
        {
            // Main panel
            _playerMainPanel = new Panel();
            _playerMainPanel.Dock = DockStyle.Fill;
            _playerMainPanel.Padding = new Padding(8);
            Controls.Add(_playerMainPanel);

            // File selection panel
            _fileSelectionPanel = new Panel();
            _fileSelectionPanel.Height = 50;
            _fileSelectionPanel.Dock = DockStyle.Top;
            _fileSelectionPanel.BorderStyle = BorderStyle.FixedSingle;
            _fileSelectionPanel.Padding = new Padding(8);

            CreateFileSelectionControls();

            // Waveform panel - fills remaining space
            _waveformPanel = new Panel();
            _waveformPanel.Dock = DockStyle.Fill;
            _waveformPanel.BorderStyle = BorderStyle.FixedSingle;
            _waveformPanel.Padding = new Padding(4);

            _waveformSeekBar = new WaveformSeekBar();
            _waveformSeekBar.Dock = DockStyle.Fill;
            _waveformPanel.Controls.Add(_waveformSeekBar);

            // Filters panel
            _filtersPanel = new Panel();
            _filtersPanel.Height = 60;
            _filtersPanel.Dock = DockStyle.Top;
            _filtersPanel.Padding = new Padding(0, 4, 0, 4);

            _frequencyFilterControl = new FrequencyFilterControl();
            _frequencyFilterControl.Dock = DockStyle.Fill;
            _filtersPanel.Controls.Add(_frequencyFilterControl);

            // Controls panel
            _controlsPanel = new Panel();
            _controlsPanel.Height = 80;
            _controlsPanel.Dock = DockStyle.Bottom;
            _controlsPanel.BorderStyle = BorderStyle.FixedSingle;

            CreatePlaybackControls();

            // Add panels to main panel in correct docking order
            _playerMainPanel.Controls.Add(_controlsPanel);  // Bottom first
            _playerMainPanel.Controls.Add(_fileSelectionPanel); // Top first
            _playerMainPanel.Controls.Add(_filtersPanel);   // Top second
            _playerMainPanel.Controls.Add(_waveformPanel);  // Fill remaining space
        }

        private void CreateFileSelectionControls()
        {
            var fileTableLayout = new TableLayoutPanel();
            fileTableLayout.Dock = DockStyle.Fill;
            fileTableLayout.ColumnCount = 3;
            fileTableLayout.RowCount = 1;
            fileTableLayout.Margin = new Padding(0);
            fileTableLayout.Padding = new Padding(0);

            fileTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fileTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fileTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fileTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _fileSelectionPanel.Controls.Add(fileTableLayout);

            _fileSelectionLabel = new Label();
            _fileSelectionLabel.Text = "Recording File:";
            _fileSelectionLabel.AutoSize = true;
            _fileSelectionLabel.Anchor = AnchorStyles.Left;
            _fileSelectionLabel.TextAlign = ContentAlignment.MiddleLeft;
            _fileSelectionLabel.Margin = new Padding(0, 0, 12, 0);
            fileTableLayout.Controls.Add(_fileSelectionLabel, 0, 0);

            _filePathTextBox = new TextBox();
            _filePathTextBox.ReadOnly = true;
            _filePathTextBox.BackColor = SystemColors.Window;
            _filePathTextBox.Dock = DockStyle.Fill;
            _filePathTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            _filePathTextBox.Margin = new Padding(0, 0, 12, 0);
            fileTableLayout.Controls.Add(_filePathTextBox, 1, 0);

            _browseButton = new Button();
            _browseButton.Text = "Browse...";
            _browseButton.Size = new Size(80, 28);
            _browseButton.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            _browseButton.Margin = new Padding(0, 0, 0, 0);
            fileTableLayout.Controls.Add(_browseButton, 2, 0);
        }

        private void CreatePlaybackControls()
        {
            // Create a table layout for organized control arrangement
            var controlsLayout = new TableLayoutPanel();
            controlsLayout.Dock = DockStyle.Fill;
            controlsLayout.ColumnCount = 4;
            controlsLayout.RowCount = 1;
            controlsLayout.Padding = new Padding(8);
            
            // Configure column styles: playback buttons | time labels | spacer | volume
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Playback buttons
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Time labels  
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Spacer
            controlsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Volume control
            
            // Row style
            controlsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            
            _controlsPanel.Controls.Add(controlsLayout);

            // Create playback buttons panel
            var playbackButtonsPanel = new FlowLayoutPanel();
            playbackButtonsPanel.FlowDirection = FlowDirection.LeftToRight;
            playbackButtonsPanel.AutoSize = true;
            playbackButtonsPanel.WrapContents = false;
            playbackButtonsPanel.Margin = new Padding(0);
            playbackButtonsPanel.Anchor = AnchorStyles.Left | AnchorStyles.Top;

            _playButton = new Button();
            _playButton.Text = "?";
            _playButton.Size = new Size(50, 32);
            _playButton.Font = new Font("Segoe UI", 12F);
            _playButton.Margin = new Padding(0, 0, 4, 0);

            _pauseButton = new Button();
            _pauseButton.Text = "?";
            _pauseButton.Size = new Size(50, 32);
            _pauseButton.Font = new Font("Segoe UI", 12F);
            _pauseButton.Enabled = false;
            _pauseButton.Margin = new Padding(0, 0, 4, 0);

            _stopButton = new Button();
            _stopButton.Text = "?";
            _stopButton.Size = new Size(50, 32);
            _stopButton.Font = new Font("Segoe UI", 12F);
            _stopButton.Enabled = false;
            _stopButton.Margin = new Padding(0, 0, 0, 0);

            playbackButtonsPanel.Controls.AddRange(new Control[] { _playButton, _pauseButton, _stopButton });

            // Create time labels panel
            var timeLabelsPanel = new FlowLayoutPanel();
            timeLabelsPanel.FlowDirection = FlowDirection.LeftToRight;
            timeLabelsPanel.AutoSize = true;
            timeLabelsPanel.WrapContents = false;
            timeLabelsPanel.Margin = new Padding(16, 0, 0, 0);
            timeLabelsPanel.Anchor = AnchorStyles.Left | AnchorStyles.Top;

            _positionLabel = new Label();
            _positionLabel.Text = "00:00";
            _positionLabel.TextAlign = ContentAlignment.MiddleCenter;
            _positionLabel.AutoSize = false;
            _positionLabel.Size = new Size(50, 32);
            _positionLabel.BorderStyle = BorderStyle.FixedSingle;
            _positionLabel.BackColor = SystemColors.Window;
            _positionLabel.Margin = new Padding(0, 0, 4, 0);

            var separatorLabel = new Label();
            separatorLabel.Text = "/";
            separatorLabel.AutoSize = true;
            separatorLabel.TextAlign = ContentAlignment.MiddleCenter;
            separatorLabel.Margin = new Padding(0, 8, 4, 0);

            _durationLabel = new Label();
            _durationLabel.Text = "00:00";
            _durationLabel.TextAlign = ContentAlignment.MiddleCenter;
            _durationLabel.AutoSize = false;
            _durationLabel.Size = new Size(50, 32);
            _durationLabel.BorderStyle = BorderStyle.FixedSingle;
            _durationLabel.BackColor = SystemColors.ControlLight;
            _durationLabel.Margin = new Padding(0, 0, 0, 0);

            timeLabelsPanel.Controls.AddRange(new Control[] { _positionLabel, separatorLabel, _durationLabel });

            // Create volume control
            _volumeControl = new VolumeControl();
            _volumeControl.Size = new Size(120, 32);
            _volumeControl.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _volumeControl.Margin = new Padding(0);

            // Add panels to main layout
            controlsLayout.Controls.Add(playbackButtonsPanel, 0, 0);
            controlsLayout.Controls.Add(timeLabelsPanel, 1, 0);
            controlsLayout.Controls.Add(_volumeControl, 3, 0);
        }



        private void SetupEventHandlers()
        {
            _playButton.Click += async (s, e) => await OnPlay();
            _pauseButton.Click += async (s, e) => await OnPause();
            _stopButton.Click += async (s, e) => await OnStop();
            _browseButton.Click += async (s, e) => await OnBrowseFile();

            _waveformSeekBar.PositionChanged += OnWaveformPositionChanged;
            _volumeControl.VolumeChanged += OnVolumeChanged;
            _frequencyFilterControl.FilterChanged += OnFrequencyFilterChanged;
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
                _audioPlaybackService.PlaybackStateChanged += OnPlaybackStateChanged;
            }
        }

        #region Event Handlers

        private async Task OnPlay()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                _uiService?.ShowWarning("Please open a recording file first.");
                return;
            }

            try
            {
                var filterConfig = _frequencyFilterControl.GetCurrentFilter();
                var audioConfig = new AudioConfig(_volumeControl.CurrentVolume);

                await _audioPlaybackService?.StartAsync(_currentFilePath, filterConfig, audioConfig)!;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting playback");
                _uiService?.ShowError($"Error starting playback: {ex.Message}");
            }
        }

        private async Task OnPause()
        {
            try
            {
                if (_audioPlaybackService?.IsPaused == true)
                {
                    await _audioPlaybackService.ResumeAsync();
                }
                else
                {
                    await _audioPlaybackService?.PauseAsync()!;
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
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping playback");
                _uiService?.ShowError($"Error stopping playback: {ex.Message}");
            }
        }

        private async Task OnBrowseFile()
        {
            try
            {
                var filePath = await _uiService?.ShowOpenFileDialogAsync(
                    "SRS Recording Files (*.raw)|*.raw|All Files (*.*)|*.*",
                    "Open SRS Recording")!;

                if (!string.IsNullOrEmpty(filePath))
                {
                    await LoadFileAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening file");
                _uiService?.ShowError($"Error opening file: {ex.Message}");
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

                if (_settingsService != null)
                {
                    _settingsService.DefaultVolume = volume;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error changing volume");
            }
        }

        private async void OnFrequencyFilterChanged(object? sender, FrequencyFilterConfig config)
        {
            try
            {
                await _audioPlaybackService?.SetFrequencyFilterAsync(config)!;
                await _waveformService?.ApplyFrequencyFilterAsync(config)!;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error applying frequency filter");
            }
        }

        private void OnPlaybackStarted(object? sender, EventArgs e)
        {
            _uiService?.InvokeOnUIThread(() =>
            {
                _playButton.Enabled = false;
                _pauseButton.Enabled = true;
                _stopButton.Enabled = true;
                _pauseButton.Text = "?";
                OnStatusChanged("Playing");
            });
        }

        private void OnPlaybackStopped(object? sender, EventArgs e)
        {
            _uiService?.InvokeOnUIThread(() =>
            {
                _playButton.Enabled = true;
                _pauseButton.Enabled = false;
                _stopButton.Enabled = false;
                _pauseButton.Text = "?";
                OnStatusChanged("Stopped");
            });
        }

        private void OnPlaybackPaused(object? sender, EventArgs e)
        {
            _uiService?.InvokeOnUIThread(() =>
            {
                _pauseButton.Text = "?";
                OnStatusChanged("Paused");
            });
        }

        private void OnPlaybackResumed(object? sender, EventArgs e)
        {
            _uiService?.InvokeOnUIThread(() =>
            {
                _pauseButton.Text = "?";
                OnStatusChanged("Playing");
            });
        }

        private void OnPlaybackError(object? sender, Exception e)
        {
            _uiService?.InvokeOnUIThread(() =>
            {
                _uiService.ShowError($"Playback error: {e.Message}");
                OnStatusChanged("Error");
            });
        }

        private void OnPlaybackStateChanged(object? sender, PlaybackState state)
        {
            _uiService?.InvokeOnUIThread(() =>
            {
                _positionLabel.Text = FormatTime(state.CurrentPosition);
                _durationLabel.Text = FormatTime(state.TotalDuration);

                if (!_waveformService?.IsUserSeeking == true)
                {
                    _waveformService?.UpdatePlaybackPosition((int)state.ProgressPercent);
                }
            });
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
        /// Sets the shared file path from another tab (to keep both tabs in sync)
        /// </summary>
        public void SetSharedFilePath(string filePath)
        {
            if (_currentFilePath == filePath) return; // Already has this file

            Logger.Debug($"Player tab receiving shared file path: {filePath}");
            
            // Suppress file change events to prevent circular updates
            _suppressFileChangeEvents = true;
            try
            {
                _ = LoadFileAsync(filePath);
            }
            finally
            {
                _suppressFileChangeEvents = false;
            }
        }

        #endregion

        private void InitializeComponent()
        {
            SuspendLayout();
            
            Name = "PlayerTabView";
            Size = new Size(800, 500);
            
            ResumeLayout(false);
        }
    }
}