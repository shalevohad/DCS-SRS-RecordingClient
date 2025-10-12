using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Extensions
{
    /// <summary>
    /// Extension methods for FrequencyModulationInfo
    /// </summary>
    public static class FrequencyModulationExtensions
    {
        // Note: GetModulationName() is already implemented in FrequencyModulationInfo class
        // and properly uses the SRS Common Modulation enum via Modulation.ToString()

        /// <summary>
        /// Get coalition display name for the primary coalition using this frequency
        /// </summary>
        public static string GetPrimaryCoalitionName(this FrequencyModulationInfo info)
        {
            var primaryCoalition = GetPrimaryCoalition(info);
            return primaryCoalition switch
            {
                "1" => "Red",
                "2" => "Blue",
                _ => "Neutral"
            };
        }

        /// <summary>
        /// Get the primary coalition for this frequency based on most active users
        /// </summary>
        public static string GetPrimaryCoalition(this FrequencyModulationInfo info)
        {
            if (info.Players == null || info.Players.Count == 0)
                return "0";

            // Group by coalition and find the one with most packets
            var coalitionPackets = new Dictionary<string, int>();
            
            foreach (var player in info.Players)
            {
                var coalition = player.Coalition ?? "0";
                coalitionPackets[coalition] = coalitionPackets.GetValueOrDefault(coalition, 0) + player.PacketCount;
            }

            var maxPackets = 0;
            var primaryCoalition = "0";
            
            foreach (var kvp in coalitionPackets)
            {
                if (kvp.Value > maxPackets)
                {
                    maxPackets = kvp.Value;
                    primaryCoalition = kvp.Key;
                }
            }

            return primaryCoalition;
        }

        /// <summary>
        /// Check if this is a valid radio frequency (not intercom or disabled)
        /// </summary>
        public static bool IsValidRadioFrequency(this FrequencyModulationInfo info)
        {
            return info.Modulation is Modulation.AM or Modulation.FM;
        }
    }
}