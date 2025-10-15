using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;

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

        /// <summary>
        /// Event fired when user seeks on the waveform
        /// </summary>
        event EventHandler<int>? SeekRequested;
    }

    /// <summary>
    /// Service contract for real-time analysis visualization
    /// </summary>
    public interface IAnalysisVisualizationService
    {
        /// <summary>
        /// Updates the frequency spectrum display
        /// </summary>
        void UpdateFrequencySpectrum(double[] frequencyData, double[] magnitudeData);

        /// <summary>
        /// Updates the transmission activity display
        /// </summary>
        void UpdateTransmissionActivity(Core.AudioPacketMetadata packet);

        /// <summary>
        /// Clears all analysis displays
        /// </summary>
        void ClearDisplays();

        /// <summary>
        /// Shows/hides the analysis panels
        /// </summary>
        void SetAnalysisVisibility(bool visible);
    }

    /// <summary>
    /// Service contract for hierarchical frequency management
    /// </summary>
    public interface IFrequencyTreeService
    {
        /// <summary>
        /// Loads frequency data and organizes it into a tree structure
        /// </summary>
        Task LoadFrequencyTreeAsync(List<FrequencyModulationInfo> frequencies);

        /// <summary>
        /// Gets the currently selected frequency/modulation combinations
        /// </summary>
        List<FrequencyModulationInfo> GetSelectedFrequencies();

        /// <summary>
        /// Sets the selection state for specific frequencies
        /// </summary>
        void SetFrequencySelection(List<FrequencyModulationInfo> selectedFrequencies);

        /// <summary>
        /// Expands/collapses frequency groups by type
        /// </summary>
        void SetGroupExpansion(string groupName, bool expanded);

        /// <summary>
        /// Event fired when frequency selection changes
        /// </summary>
        event EventHandler<List<FrequencyModulationInfo>>? SelectionChanged;
    }

    /// <summary>
    /// Service contract for managing recent files
    /// </summary>
    public interface IRecentFilesService
    {
        /// <summary>
        /// Gets the list of recent files
        /// </summary>
        List<RecentFileInfo> GetRecentFiles();

        /// <summary>
        /// Adds a file to the recent files list
        /// </summary>
        void AddRecentFile(string filePath, string displayName = "");

        /// <summary>
        /// Removes a file from recent files
        /// </summary>
        void RemoveRecentFile(string filePath);

        /// <summary>
        /// Clears all recent files
        /// </summary>
        void ClearRecentFiles();

        /// <summary>
        /// Event fired when recent files list changes
        /// </summary>
        event EventHandler? RecentFilesChanged;
    }

    /// <summary>
    /// Service contract for managing audio bookmarks
    /// </summary>
    public interface IBookmarkService
    {
        /// <summary>
        /// Adds a bookmark at the specified position
        /// </summary>
        Task AddBookmarkAsync(string filePath, TimeSpan position, string description = "");

        /// <summary>
        /// Removes a bookmark
        /// </summary>
        Task RemoveBookmarkAsync(string filePath, TimeSpan position);

        /// <summary>
        /// Gets all bookmarks for a file
        /// </summary>
        Task<List<AudioBookmark>> GetBookmarksAsync(string filePath);

        /// <summary>
        /// Updates bookmark description
        /// </summary>
        Task UpdateBookmarkAsync(string filePath, TimeSpan position, string newDescription);

        /// <summary>
        /// Event fired when bookmarks change
        /// </summary>
        event EventHandler<string>? BookmarksChanged;
    }

    /// <summary>
    /// Service contract for live audio analysis during playback
    /// </summary>
    public interface ILiveAnalysisService
    {
        /// <summary>
        /// Starts live analysis for the current playback
        /// </summary>
        void StartAnalysis();

        /// <summary>
        /// Stops live analysis
        /// </summary>
        void StopAnalysis();

        /// <summary>
        /// Processes an audio packet for analysis
        /// </summary>
        void ProcessPacket(Core.AudioPacketMetadata packet);

        /// <summary>
        /// Gets current analysis statistics
        /// </summary>
        LiveAnalysisStats GetCurrentStats();

        /// <summary>
        /// Event fired when analysis data is updated
        /// </summary>
        event EventHandler<LiveAnalysisStats>? AnalysisUpdated;
    }
}