using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using NLog;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Audio
{
    /// <summary>Handles audio processing with SRS Common integration</summary>
    public sealed class AudioProcessingEngine : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly Dictionary<string, OpusDecoder> _opusDecoders = new();
        private readonly Dictionary<string, float> _transmitterVolumes = new();
        private float _masterVolume = 1.0f;
        private bool _disposed;

        public void Initialize()
        {
            Logger.Info("Audio processing engine initialized");
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Math.Clamp(volume, 0.0f, Constants.MAX_VOLUME);
            Logger.Info($"Master volume set to {_masterVolume:F2}");
        }

        public float[] ProcessPacket(AudioPacketMetadata packet)
        {
            try
            {
                Logger.Debug($"Processing packet from {packet.TransmitterGuid}: {packet.AudioPayload?.Length ?? 0} bytes");

                // Validate input
                if (packet.AudioPayload == null || packet.AudioPayload.Length == 0)
                {
                    Logger.Warn($"Packet from {packet.TransmitterGuid} has no audio payload");
                    return new float[Constants.OPUS_FRAME_SIZE];
                }

                // Decode audio
                float[] audioData;
                if (Helpers.IsOpusEncoded(packet))
                {
                    Logger.Debug($"Decoding OPUS audio from {packet.TransmitterGuid}");
                    audioData = DecodeOpusAudio(packet);
                }
                else
                {
                    Logger.Debug($"Converting PCM audio from {packet.TransmitterGuid}");
                    audioData = Helpers.ConvertPcm16ToFloat(packet.AudioPayload);
                }

                if (audioData == null || audioData.Length == 0)
                {
                    Logger.Warn($"No audio data after decoding packet from {packet.TransmitterGuid}");
                    return new float[Constants.OPUS_FRAME_SIZE];
                }

                // Calculate original amplitude for debugging
                var originalMax = audioData.Length > 0 ? audioData.Max(Math.Abs) : 0f;

                // Resample if needed
                if (packet.SampleRate != Constants.OUTPUT_SAMPLE_RATE)
                {
                    Logger.Debug($"Resampling audio from {packet.SampleRate}Hz to {Constants.OUTPUT_SAMPLE_RATE}Hz");
                    audioData = Helpers.ResampleAudio(audioData, packet.SampleRate, Constants.OUTPUT_SAMPLE_RATE);
                }

                // Apply volume control - IMPORTANT: Ensure this doesn't zero out audio
                var effectiveVolume = GetEffectiveVolume(packet.TransmitterGuid);
                if (effectiveVolume > 0)
                {
                    ApplyVolumeControl(audioData, effectiveVolume);
                }
                else
                {
                    Logger.Warn($"Effective volume is {effectiveVolume} for {packet.TransmitterGuid} - audio will be silent");
                }

                // Apply basic audio effects (simplified)
                ApplyBasicAudioEffects(audioData, packet);

                // Log final amplitude for debugging
                var finalMax = audioData.Length > 0 ? audioData.Max(Math.Abs) : 0f;
                Logger.Debug($"Processed packet from {packet.TransmitterGuid}: {audioData.Length} samples, " +
                           $"amplitude {originalMax:F4} -> {finalMax:F4}, volume: {effectiveVolume:F2}");

                // Warn if audio is too quiet
                if (finalMax > 0 && finalMax < 0.01f)
                {
                    Logger.Warn($"Processed audio amplitude very low ({finalMax:F4}) from {packet.TransmitterGuid} - may be inaudible");
                }

                return audioData;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error processing packet from {packet.TransmitterGuid}");
                // Return silence on error to prevent audio pipeline from breaking
                return new float[Constants.OPUS_FRAME_SIZE];
            }
        }

        public void ResetDecoders()
        {
            foreach (var decoder in _opusDecoders.Values)
            {
                try
                {
                    // Reset OPUS decoder state for seeking by decoding silence with reset flag
                    var silenceBuffer = new float[Constants.OPUS_FRAME_SIZE];
                    decoder.DecodeFloat(null, silenceBuffer.AsMemory(), reset: true);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to reset OPUS decoder");
                }
            }
            Logger.Debug("All OPUS decoders reset for seeking");
        }

        private float[] DecodeOpusAudio(AudioPacketMetadata packet)
        {
            try
            {
                var decoder = GetOrCreateOpusDecoder(packet.TransmitterGuid);
                const int expectedSamples = Constants.OUTPUT_SAMPLE_RATE * Constants.OPUS_FRAME_DURATION_MS / 1000;
                var buffer = new float[expectedSamples];

                int samplesDecoded = decoder.DecodeFloat(packet.AudioPayload, buffer.AsMemory());
                
                Logger.Debug($"OPUS decode result: {samplesDecoded} samples from {packet.AudioPayload.Length} bytes for {packet.TransmitterGuid}");

                if (samplesDecoded > 0)
                {
                    if (samplesDecoded < buffer.Length)
                    {
                        Array.Resize(ref buffer, samplesDecoded);
                    }
                    
                    // Log amplitude for debugging
                    var maxAmplitude = buffer.Length > 0 ? buffer.Max(Math.Abs) : 0f;
                    Logger.Debug($"OPUS decoded amplitude: {maxAmplitude:F4} for {packet.TransmitterGuid}");
                    
                    return buffer;
                }
                else
                {
                    Logger.Warn($"OPUS decoder returned {samplesDecoded} samples for {packet.TransmitterGuid}");
                    return new float[expectedSamples];
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to decode OPUS audio for {packet.TransmitterGuid}");
                // Return silence on decode error
                return new float[Constants.OPUS_FRAME_SIZE];
            }
        }

        private OpusDecoder GetOrCreateOpusDecoder(string transmitterGuid)
        {
            if (!_opusDecoders.TryGetValue(transmitterGuid, out var decoder))
            {
                decoder = OpusDecoder.Create(Constants.OUTPUT_SAMPLE_RATE, 1);
                decoder.ForwardErrorCorrection = true;
                _opusDecoders[transmitterGuid] = decoder;
                Logger.Debug($"Created OPUS decoder for {transmitterGuid}");
            }
            return decoder;
        }

        private float GetTransmitterVolume(string transmitterGuid)
        {
            return _transmitterVolumes.TryGetValue(transmitterGuid, out var volume) ? volume : 1.0f;
        }

        private float GetEffectiveVolume(string transmitterGuid)
        {
            var transmitterVol = GetTransmitterVolume(transmitterGuid);
            var effective = transmitterVol * _masterVolume;
            
            Logger.Trace($"Effective volume for {transmitterGuid}: {transmitterVol:F2} * {_masterVolume:F2} = {effective:F2}");
            return effective;
        }

        private void ApplyVolumeControl(float[] audioBuffer, float volume)
        {
            if (Math.Abs(volume - 1.0f) < 0.001f) 
            {
                Logger.Trace("Volume is 1.0, skipping volume control");
                return;
            }

            var originalMax = audioBuffer.Length > 0 ? audioBuffer.Max(Math.Abs) : 0f;
            
            for (int i = 0; i < audioBuffer.Length; i++)
            {
                audioBuffer[i] = Math.Clamp(audioBuffer[i] * volume, -1.0f, 1.0f);
            }
            
            var newMax = audioBuffer.Length > 0 ? audioBuffer.Max(Math.Abs) : 0f;
            Logger.Trace($"Volume control applied: {volume:F2}, amplitude {originalMax:F4} -> {newMax:F4}");
        }

        private void ApplyBasicAudioEffects(float[] audioData, AudioPacketMetadata packet)
        {
            try
            {
                // Apply basic effects based on modulation type
                var modulation = (Modulation)packet.Modulation;
                
                switch (modulation)
                {
                    case Modulation.AM:
                        ApplyAmEffect(audioData);
                        break;
                    case Modulation.FM:
                        ApplyFmEffect(audioData);
                        break;
                    case Modulation.INTERCOM:
                        // Intercom is usually cleaner, apply minimal processing
                        break;
                    case Modulation.DISABLED:
                        // No effects for disabled modulation
                        break;
                    default:
                        // Unknown modulation, apply minimal AM-like effect
                        ApplyAmEffect(audioData);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply audio effects");
                // Continue without effects on error
            }
        }

        private void ApplyAmEffect(float[] audioData)
        {
            // Simple AM radio effect - add some high-frequency roll-off and slight distortion
            for (int i = 0; i < audioData.Length; i++)
            {
                // Simple high-frequency attenuation
                if (i > 0)
                {
                    audioData[i] = audioData[i] * 0.85f + audioData[i - 1] * 0.15f;
                }
                
                // Slight compression/saturation for radio effect
                var sample = audioData[i];
                audioData[i] = sample > 0 ? (float)Math.Tanh(sample * 1.2) : (float)Math.Tanh(sample * 1.2);
            }
        }

        private void ApplyFmEffect(float[] audioData)
        {
            // Simple FM radio effect - cleaner than AM but still some processing
            for (int i = 0; i < audioData.Length; i++)
            {
                // Lighter processing for FM
                if (i > 0)
                {
                    audioData[i] = audioData[i] * 0.92f + audioData[i - 1] * 0.08f;
                }
                
                // Very light compression
                var sample = audioData[i];
                audioData[i] = (float)Math.Tanh(sample * 1.05);
            }
        }

        public void SetTransmitterVolume(string transmitterGuid, float volume)
        {
            _transmitterVolumes[transmitterGuid] = Math.Clamp(volume, 0.0f, Constants.MAX_VOLUME);
            Logger.Debug($"Set volume for {transmitterGuid} to {volume:F2}");
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var decoder in _opusDecoders.Values)
            {
                try
                {
                    decoder?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error disposing OPUS decoder");
                }
            }
            _opusDecoders.Clear();

            _disposed = true;
            Logger.Debug("AudioProcessingEngine disposed");
        }
    }
}