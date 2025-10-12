using System;
using System.Threading.Tasks;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services
{
    /// <summary>
    /// Service contract for loading recording file information
    /// </summary>
    public interface IRecordingInfoService
    {
        /// <summary>
        /// Loads recording information from the specified file
        /// </summary>
        Task<RecordingFileInfo> LoadRecordingInfoAsync(string filePath);

        /// <summary>
        /// Validates if the specified file is a valid SRS recording
        /// </summary>
        bool IsValidRecordingFile(string filePath);
    }

    /// <summary>
    /// Service contract for audio playback operations
    /// </summary>
    public interface IAudioPlaybackService : IDisposable
    {
        #region Events
        event EventHandler? PlaybackStarted;
        event EventHandler? PlaybackStopped;
        event EventHandler? PlaybackPaused;
        event EventHandler? PlaybackResumed;
        event EventHandler<Exception>? PlaybackError;
        event EventHandler<PlaybackState>? PlaybackStateChanged;
        event EventHandler<Core.AudioPacketMetadata>? PacketStarted;
        #endregion

        #region Properties
        bool IsPlaying { get; }
        bool IsPaused { get; }
        TimeSpan TotalDuration { get; }
        TimeSpan CurrentPosition { get; }
        PlaybackState CurrentState { get; }
        #endregion

        #region Methods
        Task StartAsync(string filePath, FrequencyFilterConfig? frequencyFilter = null, AudioConfig? audioConfig = null);
        Task StopAsync();
        Task PauseAsync();
        Task ResumeAsync();
        Task SeekToAsync(TimeSpan position);
        Task SetVolumeAsync(float volume);
        Task SetFrequencyFilterAsync(FrequencyFilterConfig config);
        void SetUserSeeking(bool isSeeking);
        #endregion

        #region Audio Testing and Diagnostics
        /// <summary>
        /// Test method to verify audio output is working by playing a test tone
        /// </summary>
        Task PlayTestToneAsync(double frequency = 440.0, double durationSeconds = 2.0);

        /// <summary>
        /// Analyzes a recorded file for potential audio issues
        /// </summary>
        Task<string> AnalyzeRecordedFileAsync(string filePath);
        #endregion
    }

    /// <summary>
    /// Service contract for managing application settings
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets the last used file path
        /// </summary>
        string LastFilePath { get; set; }

        /// <summary>
        /// Gets the default volume setting
        /// </summary>
        float DefaultVolume { get; set; }

        /// <summary>
        /// Gets whether frequency filter should be enabled by default
        /// </summary>
        bool EnableFrequencyFilterByDefault { get; set; }

        /// <summary>
        /// Saves current settings to storage
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// Loads settings from storage
        /// </summary>
        Task LoadAsync();
    }

    /// <summary>
    /// Service contract for UI operations
    /// </summary>
    public interface IUIService
    {
        /// <summary>
        /// Shows an error message to the user
        /// </summary>
        void ShowError(string message, string title = "Error");

        /// <summary>
        /// Shows an information message to the user
        /// </summary>
        void ShowInfo(string message, string title = "Information");

        /// <summary>
        /// Shows a warning message to the user
        /// </summary>
        void ShowWarning(string message, string title = "Warning");

        /// <summary>
        /// Shows a file selection dialog
        /// </summary>
        Task<string?> ShowOpenFileDialogAsync(string filter, string title);

        /// <summary>
        /// Invokes an action on the UI thread
        /// </summary>
        void InvokeOnUIThread(Action action);
    }

    /// <summary>
    /// Service contract for waveform visualization
    /// </summary>
    public interface IWaveformService
    {
        /// <summary>
        /// Loads waveform data for the specified file
        /// </summary>
        Task LoadWaveformAsync(string filePath);

        /// <summary>
        /// Applies frequency filtering to the waveform
        /// </summary>
        Task ApplyFrequencyFilterAsync(FrequencyFilterConfig config);

        /// <summary>
        /// Clears the current waveform data
        /// </summary>
        void ClearWaveform();

        /// <summary>
        /// Updates the playback position on the waveform
        /// </summary>
        void UpdatePlaybackPosition(int position);

        /// <summary>
        /// Gets whether the user is currently seeking on the waveform
        /// </summary>
        bool IsUserSeeking { get; }
    }
}