using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.Core;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using ShalevOhad.DCS.SRS.Recorder.Core.Helpers;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.ViewModels
{
    /// <summary>
    /// Main view model that orchestrates the player functionality
    /// </summary>
    public class PlayerViewModelController : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly IRecordingInfoService _recordingInfoService;
        private readonly IAudioPlaybackService _audioPlaybackService;
        private readonly IUIService _uiService;
        private readonly IWaveformService _waveformService;
        private readonly ISettingsService _settingsService;
        
        private readonly PlayerViewModel _viewModel;
        private RecordingFileInfo? _currentRecording;
        private bool _disposed;

        #endregion

        #region Constructor

        public PlayerViewModelController(
            IRecordingInfoService recordingInfoService,
            IAudioPlaybackService audioPlaybackService,
            IUIService uiService,
            IWaveformService waveformService,
            ISettingsService settingsService)
        {
            _recordingInfoService = recordingInfoService ?? throw new ArgumentNullException(nameof(recordingInfoService));
            _audioPlaybackService = audioPlaybackService ?? throw new ArgumentNullException(nameof(audioPlaybackService));
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            _waveformService = waveformService ?? throw new ArgumentNullException(nameof(waveformService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            _viewModel = new PlayerViewModel();
            
            InitializeAsync();
            WireUpEvents();
        }

        #endregion

        #region Properties

        /// <summary>
        /// The view model for data binding
        /// </summary>
        public PlayerViewModel ViewModel => _viewModel;

        /// <summary>
        /// Information about the currently loaded recording
        /// </summary>
        public RecordingFileInfo? CurrentRecording => _currentRecording;

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads a recording file for playback
        /// </summary>
        public async Task LoadFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentException("File path cannot be empty", nameof(filePath));

                Logger.Info($"Loading recording file: {filePath}");

                _viewModel.StatusText = "Loading file...";
                _viewModel.StatusColor = System.Drawing.Color.Orange;

                // Validate file
                if (!_recordingInfoService.IsValidRecordingFile(filePath))
                {
                    _uiService.ShowError("The selected file is not a valid SRS recording file.", "Invalid File");
                    return;
                }

                // Load recording information
                var recordingInfo = await _recordingInfoService.LoadRecordingInfoAsync(filePath);
                
                if (!recordingInfo.IsValid)
                {
                    _uiService.ShowWarning("The recording file appears to be empty or corrupted.", "Empty Recording");
                    return;
                }

                // Update view model
                _currentRecording = recordingInfo;
                _viewModel.FilePath = filePath;
                _viewModel.TotalTime = FormatTime(recordingInfo.TotalDuration);
                _viewModel.InfoText = recordingInfo.FormattedInfo;
                _viewModel.IsSeekEnabled = true;
                
                // Set seek bar range
                var totalSeconds = (int)recordingInfo.TotalDuration.TotalSeconds;
                _viewModel.UpdateSeekState(0, Math.Max(1, totalSeconds * 10)); // 0.1 second precision

                // Load frequency data
                _viewModel.AvailableFrequencies = recordingInfo.FrequencyModulations;
                _viewModel.SelectedFrequencies = recordingInfo.FrequencyModulations; // Default: all selected
                _viewModel.IsFrequencyFilterEnabled = recordingInfo.FrequencyModulations.Any();

                // Load waveform
                await LoadWaveformAsync();

                // Save to settings
                _settingsService.LastFilePath = filePath;
                await _settingsService.SaveAsync();

                _viewModel.StatusText = "Ready";
                _viewModel.StatusColor = System.Drawing.SystemColors.ControlText;

                Logger.Info($"Successfully loaded recording: {recordingInfo.Statistics.TotalPackets} packets, {recordingInfo.TotalDuration}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load recording file: {filePath}");
                
                _viewModel.StatusText = "Load failed";
                _viewModel.StatusColor = System.Drawing.Color.Red;
                _viewModel.InfoText = $"Error: {ex.Message}";
                
                _uiService.ShowError($"Failed to load recording file:\n{ex.Message}");
                ResetState();
            }
        }

        /// <summary>
        /// Browses for a recording file to load
        /// </summary>
        public async Task BrowseForFileAsync()
        {
            try
            {
                var filePath = await _uiService.ShowOpenFileDialogAsync(
                    "SRS Recording Files (*.raw)|*.raw|All Files (*.*)|*.*",
                    "Select SRS Recording File");

                if (!string.IsNullOrEmpty(filePath))
                {
                    await LoadFileAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error browsing for file");
                _uiService.ShowError($"Error browsing for file: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts playback of the loaded recording
        /// </summary>
        public async Task PlayAsync()
        {
            try
            {
                if (_currentRecording == null)
                {
                    _uiService.ShowInfo("Please select a recording file first.", "No File Selected");
                    return;
                }

                if (_viewModel.IsFrequencyFilterEnabled && !_viewModel.SelectedFrequencies.Any())
                {
                    _uiService.ShowInfo("Please select at least one frequency/modulation combination to play, or disable the frequency filter.", "No Frequencies Selected");
                    return;
                }

                var frequencyFilter = _viewModel.IsFrequencyFilterEnabled 
                    ? new FrequencyFilterConfig(true, _viewModel.SelectedFrequencies)
                    : FrequencyFilterConfig.Disabled;

                var audioConfig = new AudioConfig(_viewModel.Volume);

                await _audioPlaybackService.StartAsync(_currentRecording.FilePath, frequencyFilter, audioConfig);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start playback");
                _uiService.ShowError($"Failed to start playback: {ex.Message}");
                UpdateViewModelFromPlaybackState(PlaybackState.Stopped);
            }
        }

        /// <summary>
        /// Pauses or resumes playback
        /// </summary>
        public async Task PauseResumeAsync()
        {
            try
            {
                if (_viewModel.IsPaused)
                    await _audioPlaybackService.ResumeAsync();
                else
                    await _audioPlaybackService.PauseAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to pause/resume playback");
                _uiService.ShowError($"Failed to pause/resume playback: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops playback
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                Logger.Info("Stop button clicked - stopping playback");
                await _audioPlaybackService.StopAsync();
                Logger.Debug("Playback stopped successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to stop playback");
                // Don't show error to user for stop operation - just log it
                // Stop should always succeed even if there are issues
                
                // Update the UI to reflect stopped state regardless
                _uiService.InvokeOnUIThread(() =>
                {
                    UpdateViewModelFromPlaybackState(PlaybackState.Stopped);
                });
            }
        }

        /// <summary>
        /// Seeks to a specific position
        /// </summary>
        public async Task SeekToAsync(int position)
        {
            try
            {
                if (_currentRecording == null || _viewModel.SeekMaximum <= 0)
                    return;

                var percentage = (double)position / _viewModel.SeekMaximum;
                var seekTime = TimeSpan.FromTicks((long)(percentage * _currentRecording.TotalDuration.Ticks));

                await _audioPlaybackService.SeekToAsync(seekTime);
                _viewModel.CurrentTime = FormatTime(seekTime);

                Logger.Debug($"Seeked to {seekTime} ({percentage:P2})");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to seek");
                _uiService.ShowError($"Failed to seek: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the volume setting
        /// </summary>
        public async Task SetVolumeAsync(float volume)
        {
            try
            {
                _viewModel.Volume = volume;
                await _audioPlaybackService.SetVolumeAsync(volume);
                
                // Save to settings
                _settingsService.DefaultVolume = volume;
                await _settingsService.SaveAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to set volume");
                _uiService.ShowError($"Failed to set volume: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates frequency filter settings
        /// </summary>
        public async Task UpdateFrequencyFilterAsync()
        {
            try
            {
                var config = _viewModel.IsFrequencyFilterEnabled
                    ? new FrequencyFilterConfig(true, _viewModel.SelectedFrequencies)
                    : FrequencyFilterConfig.Disabled;

                await _audioPlaybackService.SetFrequencyFilterAsync(config);
                await _waveformService.ApplyFrequencyFilterAsync(config);

                Logger.Debug($"Updated frequency filter: enabled={config.IsEnabled}, count={config.SelectedFrequencies.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to update frequency filter");
                _uiService.ShowError($"Failed to update frequency filter: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifies that user seeking has started or ended
        /// </summary>
        public void SetUserSeeking(bool isSeeking)
        {
            _viewModel.IsUserSeeking = isSeeking;
            _audioPlaybackService.SetUserSeeking(isSeeking);
        }

        #endregion

        #region Private Methods

        private async void InitializeAsync()
        {
            try
            {
                // Load settings
                await _settingsService.LoadAsync();
                _viewModel.Volume = _settingsService.DefaultVolume;

                // Load last file if it exists
                if (!string.IsNullOrEmpty(_settingsService.LastFilePath) && 
                    System.IO.File.Exists(_settingsService.LastFilePath))
                {
                    await LoadFileAsync(_settingsService.LastFilePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to initialize settings");
            }
        }

        private void WireUpEvents()
        {
            // Audio playback events
            _audioPlaybackService.PlaybackStarted += OnPlaybackStarted;
            _audioPlaybackService.PlaybackStopped += OnPlaybackStopped;
            _audioPlaybackService.PlaybackPaused += OnPlaybackPaused;
            _audioPlaybackService.PlaybackResumed += OnPlaybackResumed;
            _audioPlaybackService.PlaybackError += OnPlaybackError;
            _audioPlaybackService.PlaybackStateChanged += OnPlaybackStateChanged;
            _audioPlaybackService.PacketStarted += OnPacketStarted;

            // View model property changes
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void UnwireEvents()
        {
            if (_audioPlaybackService != null)
            {
                _audioPlaybackService.PlaybackStarted -= OnPlaybackStarted;
                _audioPlaybackService.PlaybackStopped -= OnPlaybackStopped;
                _audioPlaybackService.PlaybackPaused -= OnPlaybackPaused;
                _audioPlaybackService.PlaybackResumed -= OnPlaybackResumed;
                _audioPlaybackService.PlaybackError -= OnPlaybackError;
                _audioPlaybackService.PlaybackStateChanged -= OnPlaybackStateChanged;
                _audioPlaybackService.PacketStarted -= OnPacketStarted;
            }

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }
        }

        private async Task LoadWaveformAsync()
        {
            try
            {
                if (_currentRecording != null)
                {
                    _viewModel.StatusText = "Loading waveform...";
                    await _waveformService.LoadWaveformAsync(_currentRecording.FilePath);
                    await UpdateFrequencyFilterAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load waveform, continuing without visualization");
                _waveformService.ClearWaveform();
            }
        }

        private void ResetState()
        {
            _currentRecording = null;
            _viewModel.ResetToDefault();
            _waveformService.ClearWaveform();
        }

        private void UpdateViewModelFromPlaybackState(PlaybackState state)
        {
            _uiService.InvokeOnUIThread(() =>
            {
                _viewModel.UpdatePlaybackState(state.IsPlaying, state.IsPaused);
                
                if (state.IsPlaying)
                {
                    var statusText = _viewModel.IsFrequencyFilterEnabled
                        ? $"Playing... ({state.ProgressPercent:F1}%) - {_viewModel.SelectedFrequencies.Count}/{_viewModel.AvailableFrequencies.Count} freq/mod"
                        : $"Playing... ({state.ProgressPercent:F1}%)";
                    
                    _viewModel.StatusText = state.IsPaused ? "Paused" : statusText;
                    _viewModel.StatusColor = state.IsPaused ? System.Drawing.Color.Orange : System.Drawing.Color.Green;
                }
                else
                {
                    _viewModel.StatusText = "Ready";
                    _viewModel.StatusColor = System.Drawing.SystemColors.ControlText;
                }

                // Update progress and seek position
                _viewModel.ProgressValue = (int)state.ProgressPercent;
                
                if (!_viewModel.IsUserSeeking && _currentRecording != null)
                {
                    var percentage = state.TotalDuration.Ticks > 0 
                        ? (double)state.CurrentPosition.Ticks / state.TotalDuration.Ticks
                        : 0.0;
                    var seekPosition = (int)(percentage * _viewModel.SeekMaximum);
                    _viewModel.SeekPosition = seekPosition;
                    _waveformService.UpdatePlaybackPosition(seekPosition);
                }

                _viewModel.CurrentTime = FormatTime(state.CurrentPosition);
            });
        }

        private static string FormatTime(TimeSpan time) =>
            time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
                : $"{time.Minutes}:{time.Seconds:D2}";

        #endregion

        #region Event Handlers

        private void OnPlaybackStarted(object? sender, EventArgs e) =>
            UpdateViewModelFromPlaybackState(_audioPlaybackService.CurrentState);

        private void OnPlaybackStopped(object? sender, EventArgs e) =>
            UpdateViewModelFromPlaybackState(PlaybackState.Stopped);

        private void OnPlaybackPaused(object? sender, EventArgs e) =>
            UpdateViewModelFromPlaybackState(_audioPlaybackService.CurrentState);

        private void OnPlaybackResumed(object? sender, EventArgs e) =>
            UpdateViewModelFromPlaybackState(_audioPlaybackService.CurrentState);

        private void OnPlaybackError(object? sender, Exception ex)
        {
            Logger.Error(ex, "Playback error occurred");
            _uiService.InvokeOnUIThread(() =>
            {
                _viewModel.StatusText = "Playback error";
                _viewModel.StatusColor = System.Drawing.Color.Red;
                UpdateViewModelFromPlaybackState(PlaybackState.Stopped);
            });
        }

        private void OnPlaybackStateChanged(object? sender, PlaybackState state) =>
            UpdateViewModelFromPlaybackState(state);

        private void OnPacketStarted(object? sender, Core.AudioPacketMetadata packet)
        {
            _uiService.InvokeOnUIThread(() =>
            {
                var modulationName = Helpers.GetModulationName(packet.Modulation);
                var playerName = Helpers.GetPlayerNameWithFallback(packet.PlayerData, packet.TransmitterGuid);

                _viewModel.CurrentPacketText = $"Freq: {Helpers.FormatFrequency(packet.Frequency)} | Mod: {modulationName} | From: {playerName}";
                _viewModel.CurrentPacketTooltip = PlayerClient.Services.PlayerTooltipBuilder.BuildPacketTooltip(packet.PlayerData, packet);
            });
        }

        private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PlayerViewModel.IsFrequencyFilterEnabled):
                case nameof(PlayerViewModel.SelectedFrequencies):
                    await UpdateFrequencyFilterAsync();
                    break;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            UnwireEvents();
            _audioPlaybackService?.Dispose();
            _disposed = true;
            
            Logger.Debug("PlayerViewModelController disposed");
        }

        #endregion
    }
}