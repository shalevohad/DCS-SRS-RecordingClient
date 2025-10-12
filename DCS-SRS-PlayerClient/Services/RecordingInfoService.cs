using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShalevOhad.DCS.SRS.Recorder.Core;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services
{
    /// <summary>
    /// Service for loading and analyzing SRS recording files
    /// </summary>
    public class RecordingInfoService : IRecordingInfoService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Loads recording information from a file
        /// </summary>
        public async Task<RecordingFileInfo> LoadRecordingInfoAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException($"Recording file not found: {filePath}");
            }

            try
            {
                Logger.Info($"Loading recording info from: {filePath}");

                return await Task.Run(() => LoadRecordingInfoInternal(filePath, CancellationToken.None));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load recording info from: {filePath}");
                throw new InvalidOperationException($"Failed to load recording file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates if a file is a valid SRS recording file
        /// </summary>
        public bool IsValidRecordingFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                // Try to read at least one packet header
                return AudioPacketMetadata.TryReadMetadata(br, out var metadata) && metadata != null;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"File validation failed for: {filePath}");
                return false;
            }
        }

        private RecordingFileInfo LoadRecordingInfoInternal(string filePath, CancellationToken cancellationToken)
        {
            using var tempReader = new AudioPacketReader(filePath);

            // Get basic file information
            var packets = tempReader.ReadAllPackets(cancellationToken).ToList();
            var totalDuration = tempReader.CalculateTotalDuration();

            // Load frequency-modulation combinations
            var frequencyModulations = tempReader.GetAllFrequencyModulations(cancellationToken);

            // Calculate statistics
            var statistics = CalculateStatistics(packets);

            return new RecordingFileInfo(
                filePath,
                totalDuration,
                frequencyModulations,
                statistics
            );
        }

        private static RecordingStatistics CalculateStatistics(List<Core.AudioPacketMetadata> packets)
        {
            if (!packets.Any())
            {
                return new RecordingStatistics(0, 0, 0, 0, 0);
            }

            var uniqueFrequencies = packets.Select(p => p.Frequency).Distinct().Count();
            var uniqueModulations = packets.Select(p => p.Modulation).Distinct().Count();

            // Count unique players (using display names instead of GUIDs for better accuracy)
            var uniquePlayers = packets
                .Select(p => p.PlayerData?.GetDisplayName() ?? p.TransmitterGuid)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .Count();

            // Count unique aircraft types
            var uniqueAircraft = packets
                .Where(p => p.PlayerData?.AircraftInfo?.UnitType != null && 
                           !string.IsNullOrEmpty(p.PlayerData.AircraftInfo.UnitType))
                .Select(p => p.PlayerData!.AircraftInfo!.UnitType)
                .Distinct()
                .Count();

            return new RecordingStatistics(
                packets.Count,
                uniqueFrequencies,
                uniqueModulations,
                uniquePlayers,
                uniqueAircraft
            );
        }
    }
}