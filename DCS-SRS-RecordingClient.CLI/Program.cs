namespace ShalevOhad.DCS.SRS.Recorder.CLI
{
    using Core;
    using System;
    using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;

    class Program
    {
        static async Task Main(string[] args)
        {
            var settings = RecorderSettingsStore.Instance;

            string serverIp = args.Length > 0 ? args[0] : settings.GetRecorderSettingString(RecorderSettingKeys.ServerIp);
            int port = args.Length > 1 ? int.Parse(args[1]) : settings.GetRecorderSettingInt(RecorderSettingKeys.ServerPort);

            string clientGuid = ShortGuid.NewGuid();
            string clientName = "RecordingClient_" + clientGuid;
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
                }
                else
                {
                    isConnected = false;
                    Console.WriteLine($"[Connection Status] Disconnected. Reason: {status.Error}");

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
            };

            // Try to connect and start recording only if connected
            while (true)
            {
                Console.WriteLine("\n-----------------------------------------------------");
                Console.WriteLine($"Trying to connect to server at {serverIp} (Port:{port})...");
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
                    Console.WriteLine("to stop recording and disconnect: press Ctrl+C or close the window");
                    Console.WriteLine("-----------------------------------------------------");
                    Console.WriteLine("\nListening for incoming packets to record...");

                    recorder.PacketReceived += meta =>
                    {
                        Console.WriteLine($"Packet received: Time={meta.Timestamp}, Freq={meta.Frequency}, Mod={meta.Modulation}, TxGuid={meta.TransmitterGuid}, Size={meta.AudioPayload.Length}, Coalition={meta.Coalition}");
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
                        var key = Console.ReadKey(true);
                        if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                        {
                            lastStatus = null;
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("Exiting.");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Disconnected for non-server reason. Exiting.");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Failed to connect to server. Retry? (y/n): ");
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                    {
                        lastStatus = null;
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("Exiting.");
                        return;
                    }
                }
            }
        }
    }
}
