# DCS-SRS-RecordingClient

An open source Recording Client Integration for SRS DCS Radio.

## Essence

DCS-SRS-RecordingClient is designed to integrate with the [SRS DCS Radio](https://github.com/ciribob/DCS-SimpleRadioStandalone), providing users with the capability to record radio communications during their DCS sessions. This client is open source and aims to simplify and automate the process of capturing and storing voice transmissions within the DCS ecosystem.

## What It Brings to Users

- **Automated Recording:** Seamlessly record all SRS radio comms during your flight sessions.
- **Integration:** Designed to work smoothly with SRS and DCS World.
- **Open Source:** Modify and extend the recording functionality as needed for your group or use case.
- **User-Friendly:** Simple setup and operation.

## Minimum Requirements

- **SRS Server:** You must be connected to an SRS server version **2.3.20 or later**.

## Code Example

Here’s how you might use the RecordingClient in your environment:

```csharp
using DcsSrsRecordingClient;

// Initialize the recording client
var recorder = new SrsRecorder();

// Start recording communications
recorder.Start();

// ... DCS session ongoing ...

// Stop recording at the end of the session
recorder.Stop();

// Access recorded audio files
var files = recorder.GetRecordedFiles();
foreach (var file in files)
{
    Console.WriteLine($"Saved recording: {file}");
}
```

## Download & Installation

1. **Download**  
   - Visit the [GitHub Releases page](https://github.com/shalevohad/DCS-SRS-RecordingClient/releases) to get the latest version.
   - Download the appropriate binary for your system.

2. **Install**  
   - Extract the files to a desired folder.
   - Ensure SRS is installed and running for DCS World.

3. **Run**  
   - Execute `DCS-SRS-RecordingClient.exe`.
   - The client will automatically connect to SRS and begin recording comms.

4. **Configuration**  
   - You may specify output directories and recording parameters in the configuration file if provided.

## Contributing

Contributions are welcome! Please open issues or submit pull requests on the [GitHub page](https://github.com/shalevohad/DCS-SRS-RecordingClient).

## License

This project is open source. See the repository for details.

---
For questions or support, please visit the [repository](https://github.com/shalevohad/DCS-SRS-RecordingClient).
