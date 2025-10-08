namespace ShalevOhad.DCS.SRS.Recorder.CLI
{
    using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
    using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
    using Core;
    using NLog;
    using System;

    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static async Task Main(string[] args)
        {
            Console.WriteLine($"SRS Recording Client Version: {Constants.VERSION}");
            Console.WriteLine($"Minimum required server version: {Constants.MINIMUM_SERVER_VERSION}");
            Logger.Info($"SRS Recording Client Version: {Constants.VERSION}");
            Logger.Info($"Minimum required server version: {Constants.MINIMUM_SERVER_VERSION}");

            var settings = RecorderSettingsStore.Instance;

            string serverIp = args.Length > 0 ? args[0] : settings.GetRecorderSettingString(RecorderSettingKeys.ServerIp);
            int port = args.Length > 1 ? int.Parse(args[1]) : settings.GetRecorderSettingInt(RecorderSettingKeys.ServerPort);

            Logger.Info($"Using server IP: {serverIp}, port: {port}");

            string clientGuid = ShortGuid.NewGuid();
            string clientName = "RecordingClient_" + clientGuid;
            Logger.Info($"Generated client GUID: {clientGuid}, client name: {clientName}");
            RecordingClientState.Initialize(clientGuid, clientName);

            var recorder = new AudioPacketRecorder();
            bool isConnected = false;
            TCPClientStatusMessage? lastStatus = null;
            bool shouldReconnect = false;

            // Subscribe to connection status updates
            recorder.ConnectionStatusChanged += status =>
            {
                lastStatus = status;
                if (status.Connected)
                {
                    isConnected = true;
                    shouldReconnect = false;
                    Console.WriteLine($"[Connection Status] Connected to server: {status.Address}");
                    Logger.Info($"Connected to server: {status.Address}");

                    // Print the server version if available
                    if (!string.IsNullOrEmpty(recorder.ServerVersion))
                    {
                        Console.WriteLine($"Server version: {recorder.ServerVersion}");
                        Logger.Info($"Server version: {recorder.ServerVersion}");
                    }
                }
                else
                {
                    isConnected = false;
                    Console.WriteLine($"[Connection Status] Disconnected. Reason: {status.Error}");
                    Logger.Warn($"Disconnected. Reason: {status.Error}");

                    // Check for server-side disconnect reasons
                    if (status.Error == TCPClientStatusMessage.ErrorCode.TIMEOUT ||
                        status.Error == TCPClientStatusMessage.ErrorCode.MISMATCHED_SERVER ||
                        status.Error == TCPClientStatusMessage.ErrorCode.INVALID_SERVER)
                    {
                        shouldReconnect = true;
                    }
                    else
                    {
                        shouldReconnect = false;
                    }
                }

                if (!status.Connected)
                {
                    switch (status.Error)
                    {
                        case TCPClientStatusMessage.ErrorCode.MISMATCHED_SERVER:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error: The server version is incompatible with this client. Please update your server or client.");
                            Console.ResetColor();
                            Logger.Error("The server version is incompatible with this client. Please update your server or client.");
                            break;
                        case TCPClientStatusMessage.ErrorCode.TIMEOUT:
                            Console.WriteLine("Error: Connection timed out.");
                            Logger.Error("Connection timed out.");
                            break;
                        case TCPClientStatusMessage.ErrorCode.INVALID_SERVER:
                            Console.WriteLine("Error: Invalid server address or configuration.");
                            Logger.Error("Invalid server address or configuration.");
                            break;
                        case TCPClientStatusMessage.ErrorCode.USER_DISCONNECTED:
                            Console.WriteLine("Disconnected from server.");
                            Logger.Info("Disconnected from server.");
                            break;
                        default:
                            Console.WriteLine("Disconnected from server for an unknown reason.");
                            Logger.Warn("Disconnected from server for an unknown reason.");
                            break;
                    }
                }
            };

            // Try to connect and start recording only if connected
            while (true)
            {   
                Console.WriteLine("\n-----------------------------------------------------");
                Console.WriteLine($"Trying to connect to server at {serverIp} (Port:{port})...");
                Logger.Info($"Trying to connect to server at {serverIp} (Port:{port})...");
                await recorder.ConnectAsync(serverIp, port);

                // Wait a moment for the connection status to update
                int waitMs = 0;
                while (lastStatus == null && waitMs < 2000)
                {
                    await Task.Delay(100);
                    waitMs += 100;
                }

                if (isConnected)
                {
                    string recordingFile = settings.GetRecorderSettingString(RecorderSettingKeys.RecordingFile);
                    recorder.StartRecording(recordingFile);
                    Console.WriteLine($"Recording to file: '{recordingFile}'...");
                    Logger.Info($"Recording to file: '{recordingFile}'...");
                    Console.WriteLine("to stop recording and disconnect: press Ctrl+C or close the window");
                    Logger.Info("to stop recording and disconnect: press Ctrl+C or close the window");
                    Console.WriteLine("-----------------------------------------------------");
                    Logger.Info("-----------------------------------------------------");
                    Console.WriteLine("\nListening for incoming packets to record:");
                    Logger.Info("Listening for incoming packets to record:");

                    recorder.PacketReceived += meta =>
                    {
                        Console.WriteLine($"Packet received: Time={meta.Timestamp}, Freq={meta.Frequency}, Mod={meta.Modulation}, TxGuid={meta.TransmitterGuid}, Size={meta.AudioPayload.Length}, Coalition={meta.Coalition}");
                        Logger.Debug($"Packet received: Time={meta.Timestamp}, Freq={meta.Frequency}, Mod={meta.Modulation}, TxGuid={meta.TransmitterGuid}, Size={meta.AudioPayload.Length}, Coalition={meta.Coalition}");
                    };

                    // Handle Ctrl+C and window close
                    bool cleanedUp = false;
                    void Cleanup(object? sender, EventArgs? e)
                    {
                        if (cleanedUp) return;
                        cleanedUp = true;

                        recorder.StopRecording();
                        recorder.Disconnect();
                        Console.WriteLine("Disconnected.");
                        Logger.Info("Disconnected.");
                        Environment.Exit(0);
                    }

                    Console.CancelKeyPress += (s, e) =>
                    {
                        Cleanup(s, e);
                        e.Cancel = false;
                    };
                    AppDomain.CurrentDomain.ProcessExit += (s, e) => Cleanup(s, e);

                    // Wait until disconnected
                    while (isConnected)
                    {
                        await Task.Delay(500);
                    }

                    // If disconnected due to server issue, ask user if they want to reconnect
                    if (shouldReconnect)
                    {
                        Console.WriteLine("Lost connection due to server issue. Retry? (y/n): ");
                        Logger.Warn("Lost connection due to server issue. Retry? (y/n): ");
                        var key = Console.ReadKey(true);
                        if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                        {
                            lastStatus = null;
                            Logger.Info("User chose to retry connection.");
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("Exiting.");
                            Logger.Info("Exiting.");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Disconnected for non-server reason. Exiting.");
                        Logger.Info("Disconnected for non-server reason. Exiting.");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Failed to connect to server. Retry? (y/n): ");
                    Logger.Warn("Failed to connect to server. Retry? (y/n): ");
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                    {
                        lastStatus = null;
                        Logger.Info("User chose to retry connection.");
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("Exiting.");
                        Logger.Info("Exiting.");
                        return;
                    }
                }
            }
        }
    }
}
