using System;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using NLog;
using ShalevOhad.DCS.SRS.Recorder.Core;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    /// <summary>
    /// Centralized helper methods and utilities for the SRS Recording Client
    /// </summary>
    public static class Helpers
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Time Formatting

        /// <summary>
        /// Formats a TimeSpan into a user-friendly time string
        /// </summary>
        /// <param name="timeSpan">The TimeSpan to format</param>
        /// <returns>Formatted time string (H:MM:SS or M:SS)</returns>
        public static string FormatTime(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
            else
            {
                return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
            }
        }

        /// <summary>
        /// Formats a TimeSpan into a detailed time string with milliseconds
        /// </summary>
        /// <param name="timeSpan">The TimeSpan to format</param>
        /// <returns>Formatted time string with milliseconds</returns>
        public static string FormatTimeDetailed(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}";
            }
            else
            {
                return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}";
            }
        }

        #endregion

        #region Coalition Helpers

        /// <summary>
        /// Coalition enum compatible with SRS standards
        /// </summary>
        public enum Coalition
        {
            /// <summary>
            /// Spectator or neutral (SRS value: 0)
            /// </summary>
            Spectator = 0,
            
            /// <summary>
            /// Red coalition (SRS value: 1)
            /// </summary>
            Red = 1,
            
            /// <summary>
            /// Blue coalition (SRS value: 2)
            /// </summary>
            Blue = 2
        }

        /// <summary>
        /// Gets the coalition name from an integer value
        /// </summary>
        /// <param name="coalition">Coalition integer value</param>
        /// <returns>Coalition name string</returns>
        public static string GetCoalitionName(int coalition)
        {
            if (Enum.IsDefined(typeof(Coalition), coalition))
            {
                return ((Coalition)coalition).ToString();
            }
            
            // Fallback for unknown values (future-proofing)
            return coalition switch
            {
                1 => "Red",
                2 => "Blue",
                0 => "Spectator",
                _ => $"Unknown ({coalition})"
            };
        }

        /// <summary>
        /// Converts integer coalition value to Coalition enum safely
        /// </summary>
        /// <param name="coalitionValue">Integer coalition value</param>
        /// <returns>Coalition enum value</returns>
        public static Coalition ToCoalition(int coalitionValue)
        {
            return Enum.IsDefined(typeof(Coalition), coalitionValue) 
                ? (Coalition)coalitionValue 
                : Coalition.Spectator;
        }

        /// <summary>
        /// Gets UI color for coalition
        /// </summary>
        /// <param name="coalition">Coalition enum value</param>
        /// <returns>System.Drawing.Color for the coalition</returns>
        public static System.Drawing.Color GetCoalitionColor(Coalition coalition)
        {
            return coalition switch
            {
                Coalition.Red => System.Drawing.Color.Red,
                Coalition.Blue => System.Drawing.Color.Blue,
                Coalition.Spectator => System.Drawing.Color.Gray,
                _ => System.Drawing.SystemColors.ControlText
            };
        }

        /// <summary>
        /// Gets UI color for coalition from integer value
        /// </summary>
        /// <param name="coalitionValue">Integer coalition value</param>
        /// <returns>System.Drawing.Color for the coalition</returns>
        public static System.Drawing.Color GetCoalitionColor(int coalitionValue)
        {
            return GetCoalitionColor(ToCoalition(coalitionValue));
        }

        #endregion

        #region Modulation Helpers

        /// <summary>
        /// Gets the modulation name from a byte value
        /// </summary>
        /// <param name="modulation">Modulation byte value</param>
        /// <returns>Modulation name string</returns>
        public static string GetModulationName(byte modulation)
        {
            // Use the SRS Common Modulation enum for accurate mapping
            var mod = Enum.IsDefined(typeof(Modulation), (int)modulation) 
                ? (Modulation)modulation 
                : Modulation.DISABLED;
                
            return mod.ToString();
        }

        #endregion

        #region Frequency Helpers

        /// <summary>
        /// Converts frequency from Hz to MHz
        /// </summary>
        /// <param name="frequencyHz">Frequency in Hz</param>
        /// <returns>Frequency in MHz</returns>
        public static double HzToMHz(double frequencyHz)
        {
            return frequencyHz / 1_000_000.0;
        }

        /// <summary>
        /// Converts frequency from MHz to Hz
        /// </summary>
        /// <param name="frequencyMHz">Frequency in MHz</param>
        /// <returns>Frequency in Hz</returns>
        public static double MHzToHz(double frequencyMHz)
        {
            return frequencyMHz * 1_000_000.0;
        }

        /// <summary>
        /// Formats frequency for display
        /// </summary>
        /// <param name="frequencyHz">Frequency in Hz</param>
        /// <param name="decimalPlaces">Number of decimal places (default: 3)</param>
        /// <returns>Formatted frequency string with MHz unit</returns>
        public static string FormatFrequency(double frequencyHz, int decimalPlaces = 3)
        {
            var frequencyMhz = HzToMHz(frequencyHz);
            return $"{frequencyMhz.ToString($"F{decimalPlaces}")} MHz";
        }

        #endregion

        #region Version Helpers

        /// <summary>
        /// Compares two semantic version strings
        /// </summary>
        /// <param name="version1">First version string</param>
        /// <param name="version2">Second version string</param>
        /// <returns>True if version1 is lower than version2</returns>
        public static bool IsVersionLower(string version1, string version2)
        {
            if (Version.TryParse(version1, out var ver1) && Version.TryParse(version2, out var ver2))
            {
                return ver1 < ver2;
            }
            return false; // If parsing fails, assume not lower
        }

        /// <summary>
        /// Compares two semantic version strings
        /// </summary>
        /// <param name="version1">First version string</param>
        /// <param name="version2">Second version string</param>
        /// <returns>True if version1 is greater than version2</returns>
        public static bool IsVersionGreater(string version1, string version2)
        {
            if (Version.TryParse(version1, out var ver1) && Version.TryParse(version2, out var ver2))
            {
                return ver1 > ver2;
            }
            return false; // If parsing fails, assume not greater
        }

        #endregion

        #region String Helpers

        /// <summary>
        /// Safely truncates a string to a specified length
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="maxLength">Maximum length</param>
        /// <param name="addEllipsis">Whether to add "..." at the end if truncated</param>
        /// <returns>Truncated string</returns>
        public static string TruncateString(string input, int maxLength, bool addEllipsis = true)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input ?? string.Empty;

            if (addEllipsis && maxLength > 3)
            {
                return input[..(maxLength - 3)] + "...";
            }
            
            return input[..maxLength];
        }

        /// <summary>
        /// Safely gets a substring of a GUID for display
        /// </summary>
        /// <param name="guid">GUID string</param>
        /// <param name="length">Length to display (default: 8)</param>
        /// <returns>Truncated GUID with ellipsis</returns>
        public static string GetDisplayGuid(string guid, int length = 8)
        {
            if (string.IsNullOrEmpty(guid))
                return "Unknown";
            
            return TruncateString(guid, length, true);
        }

        #endregion

        #region Audio Helpers

        /// <summary>
        /// Converts audio sample count to duration
        /// </summary>
        /// <param name="sampleCount">Number of samples</param>
        /// <param name="sampleRate">Sample rate in Hz</param>
        /// <returns>Duration as TimeSpan</returns>
        public static TimeSpan SamplesToDuration(int sampleCount, int sampleRate)
        {
            if (sampleRate <= 0) return TimeSpan.Zero;
            
            double seconds = (double)sampleCount / sampleRate;
            return TimeSpan.FromSeconds(seconds);
        }

        /// <summary>
        /// Converts duration to sample count
        /// </summary>
        /// <param name="duration">Duration as TimeSpan</param>
        /// <param name="sampleRate">Sample rate in Hz</param>
        /// <returns>Number of samples</returns>
        public static int DurationToSamples(TimeSpan duration, int sampleRate)
        {
            return (int)(duration.TotalSeconds * sampleRate);
        }

        /// <summary>
        /// Formats audio size in human-readable format
        /// </summary>
        /// <param name="bytes">Size in bytes</param>
        /// <returns>Formatted size string</returns>
        public static string FormatAudioSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Converts 16-bit PCM audio data to float array
        /// </summary>
        /// <param name="pcmData">16-bit PCM audio data</param>
        /// <returns>Float array with values in -1.0 to 1.0 range</returns>
        public static float[] ConvertPcm16ToFloat(byte[] pcmData)
        {
            var floatData = new float[pcmData.Length / 2];
            for (int i = 0; i < floatData.Length; i++)
            {
                short pcmSample = BitConverter.ToInt16(pcmData, i * 2);
                floatData[i] = pcmSample / 32768.0f; // Convert to -1.0 to 1.0 range
            }
            return floatData;
        }

        /// <summary>
        /// Resamples audio from one sample rate to another using linear interpolation
        /// </summary>
        /// <param name="inputAudio">Input audio data</param>
        /// <param name="inputSampleRate">Input sample rate</param>
        /// <param name="outputSampleRate">Output sample rate</param>
        /// <returns>Resampled audio data</returns>
        public static float[] ResampleAudio(float[] inputAudio, int inputSampleRate, int outputSampleRate)
        {
            if (inputSampleRate == outputSampleRate)
                return inputAudio;

            // Simple linear interpolation resampling
            double ratio = (double)inputSampleRate / outputSampleRate;
            int outputLength = (int)(inputAudio.Length / ratio);
            var outputAudio = new float[outputLength];

            for (int i = 0; i < outputLength; i++)
            {
                double sourceIndex = i * ratio;
                int index1 = (int)Math.Floor(sourceIndex);
                int index2 = Math.Min(index1 + 1, inputAudio.Length - 1);
                float fraction = (float)(sourceIndex - index1);

                if (index1 < inputAudio.Length)
                {
                    outputAudio[i] = inputAudio[index1] * (1 - fraction) + 
                                   (index2 < inputAudio.Length ? inputAudio[index2] * fraction : 0);
                }
            }

            Logger.Debug($"Resampled audio from {inputSampleRate}Hz to {outputSampleRate}Hz: {inputAudio.Length} -> {outputLength} samples");
            return outputAudio;
        }

        /// <summary>
        /// Detects if audio packet contains OPUS encoded data
        /// </summary>
        /// <param name="packet">Audio packet metadata</param>
        /// <returns>True if OPUS encoded, false if PCM</returns>
        public static bool IsOpusEncoded(AudioPacketMetadata packet)
        {
            if (packet.AudioPayload == null || packet.AudioPayload.Length == 0)
                return false;

            // Multiple detection methods for robustness
            
            // Method 1: Size-based heuristic
            // PCM audio for 20ms at 48kHz mono 16-bit = 1920 bytes
            // OPUS for same duration is typically 60-200 bytes
            const int expectedPcmSize = Constants.OUTPUT_SAMPLE_RATE * Constants.OPUS_FRAME_DURATION_MS / 1000 * 2; // 1920 bytes
            const int opusMaxSize = 400; // Conservative threshold
            
            if (packet.AudioPayload.Length <= opusMaxSize && packet.AudioPayload.Length < expectedPcmSize / 3)
            {
                Logger.Debug($"Detected OPUS by size: {packet.AudioPayload.Length} bytes (expected PCM: {expectedPcmSize})");
                return true;
            }

            // Method 2: Check for OPUS header patterns (first few bytes)
            // OPUS packets often start with specific bit patterns
            if (packet.AudioPayload.Length >= 2)
            {
                byte firstByte = packet.AudioPayload[0];
                // Check for OPUS configuration bits in first byte
                // This is a simplified check - OPUS has complex headers
                if ((firstByte & 0x80) != 0) // Check if it looks like OPUS config
                {
                    Logger.Debug($"Detected OPUS by header pattern: 0x{firstByte:X2}");
                    return true;
                }
            }

            // Method 3: Fallback - assume smaller packets are OPUS
            return packet.AudioPayload.Length < 500; // Conservative threshold
        }

        /// <summary>
        /// Determines appropriate radio model based on packet modulation
        /// </summary>
        /// <param name="packet">Audio packet metadata</param>
        /// <returns>Radio model name for SRS effects pipeline</returns>
        public static string DetermineRadioModel(AudioPacketMetadata packet)
        {
            var modulation = (Modulation)packet.Modulation;
            
            // Map modulation types to appropriate radio models
            // These should match the radio models available in your SRS Common setup
            return modulation switch
            {
                Modulation.AM => "AN/PRC-152", // Common military AM radio
                Modulation.FM => "AN/PRC-148", // Common military FM radio
                Modulation.INTERCOM => "Intercom", // Aircraft intercom
                Modulation.DISABLED => "NoEffect", // No radio effects
                _ => "GenericAM" // Fallback
            };
        }

        #endregion

        #region Player Info Helpers

        /// <summary>
        /// Gets a fallback player name from PlayerInfo and GUID
        /// </summary>
        /// <param name="playerInfo">Player information</param>
        /// <param name="transmitterGuid">Transmitter GUID as fallback</param>
        /// <returns>Display name for the player</returns>
        public static string GetPlayerNameWithFallback(PlayerInfo? playerInfo, string transmitterGuid)
        {
            if (playerInfo != null && !string.IsNullOrEmpty(playerInfo.Name) && playerInfo.Name != transmitterGuid)
                return playerInfo.Name;
            
            if (!string.IsNullOrEmpty(transmitterGuid))
                return $"Unknown Player ({GetDisplayGuid(transmitterGuid)})";
            
            return "Unknown Player";
        }

        /// <summary>
        /// Gets seat information with fallback
        /// </summary>
        /// <param name="seat">Seat number</param>
        /// <returns>Formatted seat string</returns>
        public static string GetSeatWithFallback(int seat)
        {
            return seat >= 0 ? $"Seat {seat}" : "Unknown Seat";
        }

        /// <summary>
        /// Gets recording status display string
        /// </summary>
        /// <param name="allowRecord">Whether recording is allowed</param>
        /// <returns>Recording status string</returns>
        public static string GetRecordingStatus(bool allowRecord)
        {
            return allowRecord ? "Recording Allowed" : "Recording Denied";
        }

        #endregion

        #region File Helpers

        /// <summary>
        /// Gets a safe filename from a potentially unsafe string
        /// </summary>
        /// <param name="filename">Original filename</param>
        /// <returns>Safe filename with invalid characters replaced</returns>
        public static string GetSafeFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return "unnamed";

            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            var safeName = filename;
            
            foreach (char c in invalidChars)
            {
                safeName = safeName.Replace(c, '_');
            }
            
            return safeName;
        }

        /// <summary>
        /// Gets file extension based on content type
        /// </summary>
        /// <param name="isRaw">Whether the file is raw audio data</param>
        /// <returns>Appropriate file extension</returns>
        public static string GetRecordingFileExtension(bool isRaw = true)
        {
            return isRaw ? ".raw" : ".wav";
        }

        #endregion

        #region Validation Helpers

        /// <summary>
        /// Validates an IP address string
        /// </summary>
        /// <param name="ipAddress">IP address string</param>
        /// <returns>True if valid IP address</returns>
        public static bool IsValidIpAddress(string ipAddress)
        {
            return System.Net.IPAddress.TryParse(ipAddress, out _);
        }

        /// <summary>
        /// Validates a port number
        /// </summary>
        /// <param name="port">Port number</param>
        /// <returns>True if valid port (1-65535)</returns>
        public static bool IsValidPort(int port)
        {
            return port > 0 && port <= 65535;
        }

        /// <summary>
        /// Validates a frequency value
        /// </summary>
        /// <param name="frequency">Frequency in Hz</param>
        /// <returns>True if valid frequency (> 0)</returns>
        public static bool IsValidFrequency(double frequency)
        {
            return frequency > 0;
        }

        #endregion
    }
}