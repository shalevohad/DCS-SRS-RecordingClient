using System;
using System.Collections.Generic;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models
{
    /// <summary>
    /// Data transfer object for recording file information
    /// </summary>
    public record RecordingFileInfo(
        string FilePath,
        TimeSpan TotalDuration,
        List<FrequencyModulationInfo> FrequencyModulations,
        RecordingStatistics Statistics
    )
    {
        public bool IsValid => !string.IsNullOrEmpty(FilePath) && Statistics.TotalPackets > 0;
        
        public string FormattedInfo => 
            $"Duration: {FormatTime(TotalDuration)} | " +
            $"Packets: {Statistics.TotalPackets} | " +
            $"Frequencies: {Statistics.UniqueFrequencies} | " +
            $"Modulations: {Statistics.UniqueModulations} | " +
            $"Players: {Statistics.UniquePlayers}" +
            (Statistics.UniqueAircraft > 0 ? $" | Aircraft: {Statistics.UniqueAircraft}" : "");

        private static string FormatTime(TimeSpan time) =>
            time.TotalHours >= 1 
                ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
                : $"{time.Minutes}:{time.Seconds:D2}";
    }

    /// <summary>
    /// Statistics about a recording file
    /// </summary>
    public record RecordingStatistics(
        int TotalPackets,
        int UniqueFrequencies,
        int UniqueModulations,
        int UniquePlayers,
        int UniqueAircraft
    );

    /// <summary>
    /// Represents current playback state
    /// </summary>
    public record PlaybackState(
        bool IsPlaying,
        bool IsPaused,
        TimeSpan CurrentPosition,
        TimeSpan TotalDuration,
        double ProgressPercent
    )
    {
        public static PlaybackState Stopped => new(false, false, TimeSpan.Zero, TimeSpan.Zero, 0.0);
        public static PlaybackState Playing(TimeSpan current, TimeSpan total) => 
            new(true, false, current, total, total.Ticks > 0 ? (double)current.Ticks / total.Ticks * 100 : 0);
        public static PlaybackState Paused(TimeSpan current, TimeSpan total) => 
            new(true, true, current, total, total.Ticks > 0 ? (double)current.Ticks / total.Ticks * 100 : 0);
    }

    /// <summary>
    /// Configuration for frequency filtering
    /// </summary>
    public record FrequencyFilterConfig(
        bool IsEnabled,
        List<FrequencyModulationInfo> SelectedFrequencies
    )
    {
        public static FrequencyFilterConfig Disabled => new(false, new List<FrequencyModulationInfo>());
        public static FrequencyFilterConfig All(List<FrequencyModulationInfo> frequencies) => 
            new(true, frequencies);
    }

    /// <summary>
    /// Audio configuration settings
    /// </summary>
    public record AudioConfig(
        float MasterVolume
    )
    {
        public static AudioConfig Default => new(1.0f);
    }

    /// <summary>
    /// Information about a recent file
    /// </summary>
    public record RecentFileInfo(
        string FilePath,
        string DisplayName,
        DateTime LastAccessed,
        TimeSpan Duration,
        int PacketCount
    )
    {
        public bool IsValid => File.Exists(FilePath);
        public string FormattedLastAccessed => LastAccessed.ToString("yyyy-MM-dd HH:mm");
        public string FormattedDuration => Duration.TotalHours >= 1 
            ? $"{(int)Duration.TotalHours}:{Duration.Minutes:D2}:{Duration.Seconds:D2}"
            : $"{Duration.Minutes}:{Duration.Seconds:D2}";
    }

    /// <summary>
    /// Audio bookmark information
    /// </summary>
    public record AudioBookmark(
        string FilePath,
        TimeSpan Position,
        string Description,
        DateTime Created
    )
    {
        public string FormattedPosition => Position.TotalHours >= 1 
            ? $"{(int)Position.TotalHours}:{Position.Minutes:D2}:{Position.Seconds:D2}"
            : $"{Position.Minutes}:{Position.Seconds:D2}";
        public string FormattedCreated => Created.ToString("yyyy-MM-dd HH:mm");
    }

    /// <summary>
    /// Live analysis statistics
    /// </summary>
    public record LiveAnalysisStats(
        int ProcessedPackets,
        Dictionary<double, int> FrequencyActivity,
        Dictionary<string, int> PlayerActivity,
        Dictionary<string, int> ModulationActivity,
        TimeSpan AnalysisDuration,
        double AveragePacketsPerSecond
    )
    {
        public static LiveAnalysisStats Empty => new(
            0, 
            new Dictionary<double, int>(), 
            new Dictionary<string, int>(), 
            new Dictionary<string, int>(), 
            TimeSpan.Zero, 
            0.0
        );
    }

    /// <summary>
    /// Waveform data for visualization
    /// </summary>
    public record WaveformData(
        float[] Peaks,
        float[] RMS,
        TimeSpan Duration,
        int SampleRate
    )
    {
        public int SamplesPerPixel => Peaks.Length > 0 ? (int)(Duration.TotalSeconds * SampleRate / Peaks.Length) : 1;
    }

    /// <summary>
    /// Frequency group information for tree view
    /// </summary>
    public record FrequencyGroup(
        string Name,
        List<FrequencyModulationInfo> Frequencies,
        bool IsExpanded = true
    )
    {
        public int Count => Frequencies.Count;
        public string DisplayText => $"{Name} ({Count})";
    }

    /// <summary>
    /// Enhanced file management information
    /// </summary>
    public record FileManagementInfo(
        List<RecentFileInfo> RecentFiles,
        List<AudioBookmark> Bookmarks,
        List<string> FavoriteFiles
    )
    {
        public static FileManagementInfo Empty => new(
            new List<RecentFileInfo>(),
            new List<AudioBookmark>(),
            new List<string>()
        );
    }

    /// <summary>
    /// Batch operation information
    /// </summary>
    public record BatchOperationInfo(
        List<string> SelectedFiles,
        string OperationType,
        Dictionary<string, object> Parameters
    )
    {
        public static BatchOperationInfo Empty => new(
            new List<string>(),
            string.Empty,
            new Dictionary<string, object>()
        );
    }

    /// <summary>
    /// Enhanced analysis configuration
    /// </summary>
    public record AnalysisConfig(
        bool EnableRealTimeAnalysis,
        bool ShowFrequencyActivity,
        bool ShowPlayerActivity,
        bool ShowModulationActivity,
        TimeSpan AnalysisWindow
    )
    {
        public static AnalysisConfig Default => new(
            true,
            true,
            true,
            true,
            TimeSpan.FromSeconds(30)
        );
    }

    /// <summary>
    /// Playback session information for enhanced features
    /// </summary>
    public record PlaybackSession(
        string FilePath,
        DateTime StartTime,
        TimeSpan CurrentPosition,
        FrequencyFilterConfig FilterConfig,
        AudioConfig AudioConfig,
        List<AudioBookmark> SessionBookmarks
    )
    {
        public TimeSpan SessionDuration => DateTime.Now - StartTime;
        public string FormattedSessionTime => SessionDuration.ToString(@"hh\:mm\:ss");
    }
}