using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    public class AudioPacketRecorder
    {
        private TCPClientHandler? _tcpClientHandler;
        private UDPVoiceHandler? _udpVoiceHandler;
        private FileStream? _fileStream;
        private CancellationTokenSource? _recordingCts;
        private string? _outputFile;
        private string? _clientGuid;
        private IPEndPoint? _serverEndpoint;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private int _sampleRate = Constants.OUTPUT_SAMPLE_RATE; // Usually 48000
        private int _channelCount = 1; // Mono (default for SRS voice)

        public bool IsConnected => _tcpClientHandler?.TCPConnected ?? false;

        /// <summary>
        /// Connects to the SRS server using TCP for control and UDP for audio.
        /// </summary>
        public void Connect(string serverIp, int tcpPort, int udpPort, string clientGuid, SRClientBase clientState)
        {
            try
            {
                _clientGuid = clientGuid;
                _serverEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), udpPort);

                // TCP for control
                var tcpEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), tcpPort);
                _tcpClientHandler = new TCPClientHandler(clientGuid, clientState);
                _tcpClientHandler.TryConnect(tcpEndpoint);

                // UDP for audio
                _udpVoiceHandler = new UDPVoiceHandler(clientGuid, _serverEndpoint);
                _udpVoiceHandler.Connect();

                Logger.Info("Connected to SRS server (TCP/UDP).");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to connect to SRS server.");
                throw;
            }
        }

        public void Disconnect()
        {
            Logger.Info("Disconnecting from SRS server.");

            _tcpClientHandler?.Disconnect();
            StopRecording();
            _udpVoiceHandler?.RequestStop();
            _udpVoiceHandler = null;
        }

        /// <summary>
        /// Starts recording incoming UDP audio packets to a raw file.
        /// </summary>
        public void StartRecording(string filePath)
        {
            if (_udpVoiceHandler == null)
                throw new InvalidOperationException("UDPVoiceHandler not initialized.");

            _outputFile = filePath;
            _fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            _recordingCts = new CancellationTokenSource();
            Task.Run(() => RecordingLoop(_recordingCts.Token));
        }

        public void StopRecording()
        {
            _recordingCts?.Cancel();
            _fileStream?.Dispose();
            _fileStream = null;
        }

        private async Task RecordingLoop(CancellationToken token)
        {
            if (_udpVoiceHandler == null)
                return;

            // EncodedAudio is a BlockingCollection<byte[]> containing received UDP packets
            while (!token.IsCancellationRequested)
            {
                try
                {
                    byte[]? packet = null;
                    // Try to take a packet with a timeout to allow cancellation
                    if (_udpVoiceHandler.EncodedAudio.TryTake(out packet, 100))
                    {
                        if (packet != null && packet.Length > 0)
                        {
                            var meta = ExtractAudioMetadata(packet);

                            // Use TryWriteMetadata to serialize metadata and audio payload
                            using var bw = new BinaryWriter(_fileStream!, System.Text.Encoding.UTF8, leaveOpen: true);
                            if (!meta.TryWriteMetadata(bw))
                            {
                                Logger.Error("Failed to write audio packet metadata.");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.Info("Recording cancelled.");
                    break;
                }
                catch (IOException ioEx)
                {
                    Logger.Error(ioEx, "IO error while writing audio packet.");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Unexpected error during audio packet reception.");
                }
            }
        }

        // Extract metadata from UDP packet
        private AudioPacketMetadata ExtractAudioMetadata(byte[] packet)
        {
            int offset = 0;
            ushort packetLength = BitConverter.ToUInt16(packet, offset); offset += 2;
            ushort audioPart1Length = BitConverter.ToUInt16(packet, offset); offset += 2;
            ushort freqPartLength = BitConverter.ToUInt16(packet, offset); offset += 2;

            byte[] audioPayload = new byte[audioPart1Length];
            Array.Copy(packet, offset, audioPayload, 0, audioPart1Length);
            offset += audioPart1Length;

            double frequency = BitConverter.ToDouble(packet, offset); offset += 8;
            byte modulation = packet[offset]; offset += 1;
            byte encryption = packet[offset]; offset += 1;

            uint transmitterUnitId = BitConverter.ToUInt32(packet, offset); offset += 4;
            ulong packetId = BitConverter.ToUInt64(packet, offset); offset += 8;
            byte hopCount = packet[offset]; offset += 1;

            string transmitterGuid = System.Text.Encoding.ASCII.GetString(packet, offset, 22); offset += 22;

            return new AudioPacketMetadata(
                DateTime.UtcNow,
                frequency,
                modulation,
                encryption,
                transmitterUnitId,
                packetId,
                transmitterGuid,
                _sampleRate,
                _channelCount,
                audioPayload
            );
        }
    }
}