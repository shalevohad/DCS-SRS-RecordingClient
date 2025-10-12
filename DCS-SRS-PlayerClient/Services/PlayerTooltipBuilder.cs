using System;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using ShalevOhad.DCS.SRS.Recorder.Core;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services
{
    /// <summary>
    /// Service for building tooltip text from player information
    /// </summary>
    public static class PlayerTooltipBuilder
    {
        /// <summary>
        /// Builds a tooltip string for the current packet display
        /// </summary>
        public static string BuildPacketTooltip(PlayerInfo? playerInfo, AudioPacketMetadata packet)
        {
            if (playerInfo == null)
            {
                return BuildLegacyTooltip(packet);
            }

            return BuildEnhancedTooltip(playerInfo, packet);
        }

        /// <summary>
        /// Builds a tooltip for player frequency information
        /// </summary>
        public static string BuildPlayerFrequencyTooltip(PlayerFrequencyInfo player)
        {
            return $"Player: {player.Name}\n" +
                   $"Coalition: {player.Coalition}\n" +
                   $"Aircraft: {player.Aircraft}\n" +
                   $"First Seen: {player.FirstSeen:HH:mm:ss}\n" +
                   $"Last Seen: {player.LastSeen:HH:mm:ss}\n" +
                   $"Packets: {player.PacketCount}";
        }

        private static string BuildLegacyTooltip(AudioPacketMetadata packet)
        {
            return $"Legacy Recording (Limited Info):\n" +
                   $"Transmitter GUID: {packet.TransmitterGuid}\n" +
                   $"Coalition: {Helpers.GetCoalitionName(packet.Coalition)}\n" +
                   $"Frequency: {packet.Frequency / 1_000_000.0:F3} MHz\n" +
                   $"Modulation: {Helpers.GetModulationName(packet.Modulation)}\n" +
                   $"Timestamp: {packet.Timestamp:HH:mm:ss}\n" +
                   $"Audio Size: {packet.AudioPayload?.Length ?? 0} bytes";
        }

        private static string BuildEnhancedTooltip(PlayerInfo playerInfo, AudioPacketMetadata packet)
        {
            string playerName = GetPlayerNameWithFallback(playerInfo, packet.TransmitterGuid);
            string coalition = GetCoalitionWithFallback(playerInfo, packet.Coalition);
            string seat = GetSeatWithFallback(playerInfo);
            string aircraft = GetAircraftWithFallback(playerInfo);
            string position = GetPositionWithFallback(playerInfo);
            string recordStatus = GetRecordStatusWithFallback(playerInfo);
            string recordingType = DetermineRecordingType(playerInfo);

            return $"{recordingType}:\n" +
                   $"Player: {playerName}\n" +
                   $"Coalition: {coalition}\n" +
                   $"Seat: {seat}\n" +
                   $"Aircraft: {aircraft}\n" +
                   $"Position: {position}\n" +
                   $"Recording: {recordStatus}\n" +
                   $"Frequency: {packet.Frequency / 1_000_000.0:F3} MHz\n" +
                   $"Timestamp: {packet.Timestamp:HH:mm:ss}";
        }

        private static string GetPlayerNameWithFallback(PlayerInfo playerInfo, string transmitterGuid)
        {
            if (!string.IsNullOrEmpty(playerInfo.Name) && playerInfo.Name != transmitterGuid)
                return playerInfo.Name;

            if (!string.IsNullOrEmpty(transmitterGuid))
                return $"Unknown Player ({transmitterGuid[..Math.Min(8, transmitterGuid.Length)]})";

            return "Unknown Player";
        }

        private static string GetCoalitionWithFallback(PlayerInfo playerInfo, int packetCoalition)
        {
            int coalitionValue = playerInfo.Coalition != 0 ? playerInfo.Coalition : packetCoalition;
            return Helpers.GetCoalitionName(coalitionValue);
        }

        private static string GetSeatWithFallback(PlayerInfo playerInfo)
        {
            if (playerInfo.Seat >= 0)
                return $"Seat {playerInfo.Seat}";

            return "Seat Unknown";
        }

        private static string GetAircraftWithFallback(PlayerInfo playerInfo)
        {
            if (playerInfo.AircraftInfo?.UnitType != null && !string.IsNullOrEmpty(playerInfo.AircraftInfo.UnitType))
            {
                string unitInfo = playerInfo.AircraftInfo.UnitType;
                if (playerInfo.AircraftInfo.UnitId > 0)
                    unitInfo += $" (ID: {playerInfo.AircraftInfo.UnitId})";
                return unitInfo;
            }

            if (playerInfo.AircraftInfo?.UnitId > 0)
                return $"Unknown Aircraft (ID: {playerInfo.AircraftInfo.UnitId})";

            return "Aircraft Unknown";
        }

        private static string GetPositionWithFallback(PlayerInfo playerInfo)
        {
            if (playerInfo.Position?.IsValid() == true)
            {
                return $"Lat: {playerInfo.Position.Latitude:F5}, Lng: {playerInfo.Position.Longitude:F5}, Alt: {playerInfo.Position.Altitude:F0}m";
            }

            if (playerInfo.Position != null &&
                (playerInfo.Position.Latitude != 0 || playerInfo.Position.Longitude != 0 || playerInfo.Position.Altitude != 0))
            {
                return $"Partial Position - Lat: {playerInfo.Position.Latitude:F5}, Lng: {playerInfo.Position.Longitude:F5}, Alt: {playerInfo.Position.Altitude:F0}m";
            }

            return "Position Unknown";
        }

        private static string GetRecordStatusWithFallback(PlayerInfo playerInfo)
        {
            return playerInfo.AllowRecord ? "Recording Allowed" : "Recording Denied";
        }

        private static string DetermineRecordingType(PlayerInfo playerInfo)
        {
            bool hasName = !string.IsNullOrEmpty(playerInfo.Name) && playerInfo.Name != playerInfo.TransmitterGuid;
            bool hasAircraft = !string.IsNullOrEmpty(playerInfo.AircraftInfo?.UnitType);
            bool hasPosition = playerInfo.Position?.IsValid() == true;
            bool hasSeat = playerInfo.Seat >= 0;

            if (hasName && hasAircraft && hasPosition && hasSeat)
                return "Complete Player Info";

            if (hasName && (hasAircraft || hasPosition))
                return "Detailed Player Info";

            if (hasName)
                return "Basic Player Info";

            return "Limited Player Info";
        }
    }
}