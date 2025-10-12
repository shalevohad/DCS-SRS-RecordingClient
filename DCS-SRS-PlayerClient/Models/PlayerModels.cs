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
}