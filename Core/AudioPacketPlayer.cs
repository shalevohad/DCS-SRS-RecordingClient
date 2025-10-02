using System;
using System.IO;
using System.Threading;
using NAudio.Wave;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    public class AudioPacketPlayer
    {
        private readonly string _filePath;
        private readonly ClientEffectsPipeline _effectsPipeline;

        public AudioPacketPlayer(string filePath)
        {
            _filePath = filePath;
            _effectsPipeline = new ClientEffectsPipeline();
        }

        public void PlayAll(CancellationToken cancellationToken = default)
        {
            var reader = new AudioPacketReader(_filePath);

            foreach (var packet in reader.ReadAllPackets(cancellationToken))
            {
                //PlayPacketWithEffects(packet, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    break;
            }
        }

        //private void PlayPacketWithEffects(AudioPacketMetadata packet, CancellationToken cancellationToken)
        //{
        //    if (packet.AudioPayload == null || packet.AudioPayload.Length == 0)
        //        return;

        //    // Convert byte[] PCM 16-bit to float[]
        //    float[] floatBuffer = ConvertPcm16ToFloat(packet.AudioPayload);

        //    // Prepare TransmissionSegment for effects pipeline
        //    var transmission = new DeJitteredTransmission
        //    {
        //        PCMMonoAudio = floatBuffer,
        //        PCMAudioLength = floatBuffer.Length,
        //        Modulation = (Modulation)packet.Modulation,
        //        Frequency = packet.Frequency,
        //        Decryptable = true
        //    };
        //    var segment = new TransmissionSegment(transmission);

        //    // Process with SRS effects pipeline (in-place on floatBuffer)
        //    _effectsPipeline.ProcessSegments(floatBuffer, 0, floatBuffer.Length, new[] { segment });

        //    // Play processed audio as 32-bit float PCM
        //    var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(packet.SampleRate, packet.ChannelCount);
        //    using var ms = new MemoryStream();
        //    using var waveOut = new WaveOutEvent();

        //    // Write float[] to stream as 32-bit PCM
        //    var buffer = new byte[floatBuffer.Length * 4];
        //    Buffer.BlockCopy(floatBuffer, 0, buffer, 0, buffer.Length);
        //    ms.Write(buffer, 0, buffer.Length);
        //    ms.Position = 0;

        //    using var waveProvider = new RawSourceWaveStream(ms, waveFormat);
        //    waveOut.Init(waveProvider);
        //    waveOut.Play();

        //    while (waveOut.PlaybackState == PlaybackState.Playing)
        //    {
        //        if (cancellationToken.IsCancellationRequested)
        //        {
        //            waveOut.Stop();
        //            break;
        //        }
        //        Thread.Sleep(10);
        //    }
        //}

        private float[] ConvertPcm16ToFloat(byte[] pcmData)
        {
            int samples = pcmData.Length / 2;
            float[] floatBuffer = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                short sample = BitConverter.ToInt16(pcmData, i * 2);
                floatBuffer[i] = sample / 32768f;
            }
            return floatBuffer;
        }
    }
}