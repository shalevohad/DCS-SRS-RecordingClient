using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Filtering
{
    /// <summary>Handles frequency filtering with modulation support</summary>
    public sealed class FrequencyFilter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private HashSet<(double Frequency, Modulation Modulation)> _selectedCombinations = new();
        private bool _enabled;

        public bool IsEnabled => _enabled;
        public IReadOnlySet<(double Frequency, Modulation Modulation)> SelectedCombinations => _selectedCombinations;

        public void SetFilter(IEnumerable<FrequencyModulationInfo> frequencyModulations)
        {
            _selectedCombinations = new HashSet<(double, Modulation)>(
                frequencyModulations.Select(fm => (fm.Frequency, fm.Modulation))
            );
            _enabled = _selectedCombinations.Count > 0;
            
            Logger.Debug($"Frequency filter set with {_selectedCombinations.Count} combinations");
        }

        public void ClearFilter()
        {
            _selectedCombinations.Clear();
            _enabled = false;
            
            Logger.Debug("Frequency filter cleared");
        }

        public bool ShouldIncludePacket(AudioPacketMetadata packet)
        {
            if (!_enabled) return true;

            var modulation = Enum.IsDefined(typeof(Modulation), (int)packet.Modulation) 
                ? (Modulation)packet.Modulation 
                : Modulation.DISABLED;

            var shouldInclude = _selectedCombinations.Contains((packet.Frequency, modulation));
            
            if (!shouldInclude)
            {
                Logger.Trace($"Packet filtered out: {packet.Frequency}Hz, {modulation}");
            }
            
            return shouldInclude;
        }
    }
}