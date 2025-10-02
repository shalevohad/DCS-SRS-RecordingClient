using Caliburn.Micro; // For IHandle<T>
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using NLog;
using System.Net;
using System.Collections.Concurrent;
using SRSTCPClientStatusMessage = Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages.TCPClientStatusMessage;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    public class AudioPacketRecorder : IHandle<SRSTCPClientStatusMessage>
    {
        private TCPClientHandler? _tcpClientHandler;
        private UDPVoiceHandler? _udpVoiceHandler;
        private FileStream? _fileStream;
        private CancellationTokenSource? _recordingCts;
        private string? _outputFile;
        private string? _clientGuid;
        private IPEndPoint? _serverUdpEndpoint;
        private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private int _sampleRate = Constants.OUTPUT_SAMPLE_RATE; // Usually 48000
        private int _channelCount = 1; // Mono (default for SRS voice)

        private readonly ConcurrentQueue<AudioPacketMetadata> _writeQueue = new();
        private Task? _writerTask;
        private readonly object _fileWriteLock = new();
        private bool _writerRunning = false;

        public bool IsConnected => _tcpClientHandler?.TCPConnected ?? false;

        /// <summary>
        /// Connects to the SRS server using TCP for control and UDP for audio.
        /// Uses RecorderSettingsStore for default values if parameters are not provided.
        /// Uses RecordingClientState.Instance for client state and GUID.
        /// </summary>

        public void Disconnect()
        {
            Logger.Info("Disconnecting from SRS server.");

            _tcpClientHandler?.Disconnect();
            StopRecording();
            _udpVoiceHandler?.RequestStop();
            _udpVoiceHandler = null;
            EventBus.Instance.Unsubcribe(this);

            ConnectionStatusChanged?.Invoke(
                new SRSTCPClientStatusMessage(
                    false,
                    SRSTCPClientStatusMessage.ErrorCode.USER_DISCONNECTED
                )
            );
        }

        /// <summary>
        /// Starts recording incoming UDP audio packets to a raw file.
        /// Uses RecorderSettingsStore for default file path if not provided.
        /// </summary>
        public void StartRecording(string? filePath = null)
        {
            // Prevent starting if already recording
            if (_recordingCts != null && !_recordingCts.IsCancellationRequested)
                return;

            if (_udpVoiceHandler == null)
                throw new InvalidOperationException("UDPVoiceHandler not initialized.");

            var settings = RecorderSettingsStore.Instance;
            _outputFile = filePath ?? settings.GetRecorderSettingString(RecorderSettingKeys.RecordingFile);
            _fileStream = new FileStream(_outputFile, FileMode.Create, FileAccess.Write);
            _recordingCts = new CancellationTokenSource();

            _writerRunning = true;
            _writerTask = Task.Run(() => WriterLoop(_recordingCts.Token));

            Task.Run(() => RecordingLoop(_recordingCts.Token));
        }

        public void StopRecording()
        {
            _recordingCts?.Cancel();
            _writerRunning = false;
            try
            {
                _writerTask?.Wait();
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    Logger.Error(ex, "Error during writer task shutdown.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during writer task shutdown.");
            }
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
                            // Notify listeners (CLI) about the received packet
                            PacketReceived?.Invoke(meta);
                            _writeQueue.Enqueue(meta);
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

            int coalition = BitConverter.ToInt32(packet, offset); offset += 4;

            // Check if the client allows recording
            bool allowRecord = true;
            var clients = ConnectedClientsSingleton.Instance;
            var client = clients.TryGetValue(transmitterGuid, out var foundClient) ? foundClient : null;
            if (client != null && client is SRClientBase srClient)
            {
                allowRecord = srClient.AllowRecord;
            }

            // Only add audioPayload if allowed
            byte[] payloadToWrite = allowRecord ? audioPayload : Array.Empty<byte>();

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
                coalition,
                payloadToWrite
            );
        }

        public event Action<AudioPacketMetadata>? PacketReceived;
        public event Action<SRSTCPClientStatusMessage>? ConnectionStatusChanged;

        public async Task HandleAsync(SRSTCPClientStatusMessage status, CancellationToken cancellationToken)
        {
            Logger.Info($"[EventBus] Received TCPClientStatusMessage: Connected={status.Connected}, Error={status.Error}");
            if (!status.Connected)
            {
                Logger.Warn($"Disconnected from server. Reason: {status.Error}");

                // Stop recording and clean up UDP
                StopRecording();
                _udpVoiceHandler?.RequestStop();
                _udpVoiceHandler = null;

                // Notify listeners (CLI/UI)
                ConnectionStatusChanged?.Invoke(status);
            }
            else
            {
                // Connected: start UDP if not already started
                if (_udpVoiceHandler == null && _serverUdpEndpoint != null)
                {
                    _udpVoiceHandler = new UDPVoiceHandler(_clientGuid, _serverUdpEndpoint);
                    _udpVoiceHandler.Connect();
                }
                ConnectionStatusChanged?.Invoke(status);
            }
        }

        public async Task ConnectAsync(
            string? serverIp = null,
            int? tcpPort = null,
            int? udpPort = null)
        {
            var settings = RecorderSettingsStore.Instance;
            var state = RecordingClientState.Instance;
            _clientGuid = state.ClientGuid;

            string ip = serverIp ?? settings.GetRecorderSettingString(RecorderSettingKeys.ServerIp);
            int tcp = tcpPort ?? settings.GetRecorderSettingInt(RecorderSettingKeys.TcpPort);
            int udp = udpPort ?? settings.GetRecorderSettingInt(RecorderSettingKeys.UdpPort);

            _serverUdpEndpoint = new IPEndPoint(IPAddress.Parse(ip), udp);

            // Subscribe to EventBus for SRSTCPClientStatusMessage events
            EventBus.Instance.SubscribeOnPublishedThread(this);

            var tcpEndpoint = new IPEndPoint(IPAddress.Parse(ip), tcp);
            _tcpClientHandler = new TCPClientHandler(_clientGuid, state);
            _tcpClientHandler.TryConnect(tcpEndpoint);
        }

        private async Task WriterLoop(CancellationToken token)
        {
            while (_writerRunning && !token.IsCancellationRequested)
            {
                if (_writeQueue.TryDequeue(out var meta))
                {
                    try
                    {
                        lock (_fileWriteLock)
                        {
                            using var bw = new BinaryWriter(_fileStream!, System.Text.Encoding.UTF8, leaveOpen: true);
                            meta.TryWriteMetadata(bw);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error writing audio packet metadata from queue.");
                    }
                }
                else
                {
                    await Task.Delay(10, token); // Avoid busy wait
                }
            }
        }
    }
}