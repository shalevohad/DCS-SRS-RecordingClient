# Audio Test and Diagnostics Features

The DCS SRS Recording Client now includes comprehensive audio testing and diagnostic capabilities to help troubleshoot audio playback issues.

## ?? **No Audio at All? Start Here!**

### **Quick Audio Test (Recommended First Step)**

**Option 1: Command Line Test**
```bash
# Test your audio system first
DCS-SRS-RecordingClient.exe --test-audio

# This will:
# - Check your audio devices
# - Test multiple audio output methods
# - Play test tones you should be able to hear
```

**Option 2: Standalone Test Utility**
- Run `AudioTestConsole.exe` for step-by-step audio system diagnosis
- Tests all available audio output methods
- Provides detailed system information

**Option 3: Player Client Test Tab**
- Open the Player Client ? "Audio Test" tab
- Click "?? Play Test Tone"
- Should hear a 440Hz tone

### **If You Can't Hear Test Tones**

This indicates a **system audio problem**, not an SRS recording issue:

1. **Check Physical Connections**:
   - Verify speakers/headphones are plugged in
   - Check volume knobs on speakers
   - Try different speakers/headphones

2. **Check Windows Audio Settings**:
   - Right-click speaker icon ? "Open Volume mixer"
   - Ensure applications aren't muted
   - Check default audio device in Settings

3. **Restart Windows Audio Service**:
   ```cmd
   # Run as Administrator
   net stop AudioSrv
   net start AudioSrv
   ```

4. **Try Different Audio Devices**:
   - Switch to different speakers/headphones in Windows settings
   - Test with built-in speakers vs external devices

5. **Update Audio Drivers**:
   - Check Device Manager for audio driver issues
   - Download latest drivers from manufacturer

### **If You CAN Hear Test Tones**

Your audio system works! The problem is with SRS recording playback:

1. **Analyze your recording file** (see File Analysis section below)
2. **Check for silent recordings** (no voice data was captured)
3. **Try boosting volume** to 150-200% in the Audio Test tab
4. **Enable debug logging** to see detailed processing information

---

## Player Client - Audio Test Tab

### Features

#### ?? **Audio System Test**
- **Test Tone Generator**: Play test tones at different frequencies (220Hz, 440Hz, 880Hz, 1000Hz)
- **Duration Control**: Set test tone duration from 1-10 seconds
- **Real-time Status**: Get immediate feedback on test results
- **System Diagnosis**: Automatic detection of audio system issues

#### ?? **Audio Settings**
- **Master Volume Control**: Adjust playback volume from 0-200%
- **Debug Logging**: Enable/disable detailed audio logging
- **Clear Logs**: Reset log files for debugging

#### ?? **File Analysis & Export**
- **File Analysis**: Deep analysis of recorded files to detect audio issues
- **WAV Export**: Export recorded audio to standard WAV format for external analysis
- **Issue Detection**: Automatically identify common problems:
  - Silent recordings
  - Low audio levels
  - Buffer issues
  - Format problems

### How to Use

1. **Test Your Audio System**:
   - Switch to the "Audio Test" tab
   - Select frequency and duration
   - Click "?? Play Test Tone"
   - Verify you can hear the tone through your speakers/headphones

2. **Analyze Recording Files**:
   - Load a recording file using "Browse..."
   - Click "?? Analyze Current File"
   - Review the analysis results for potential issues

3. **Export Audio for External Analysis**:
   - Click "?? Export to WAV"
   - Choose output location
   - Open the WAV file in audio software like Audacity

## Command Line Interface - Audio Commands

### Test Audio Output
```bash
# Basic test with system diagnosis
DCS-SRS-RecordingClient.exe --test-audio

# Custom frequency and duration
DCS-SRS-RecordingClient.exe --test-audio --frequency 1000 --duration 5
```

### Analyze Recording Files
```bash
# Analyze a recording file
DCS-SRS-RecordingClient.exe --analyze recording.raw

# Analyze and export to WAV
DCS-SRS-RecordingClient.exe --analyze recording.raw --export output.wav
```

### Get Help
```bash
DCS-SRS-RecordingClient.exe --help
```

## Standalone Audio Test Utility

For isolated audio testing, use `AudioTestConsole.exe`:

1. **System Diagnosis**: Checks audio devices, services, and volume levels
2. **Test Tone Playback**: Plays test tones using multiple audio methods
3. **Method Testing**: Tests WASAPI, DirectSound, and WaveOut separately
4. **Interactive Feedback**: Asks if you heard the tones to confirm working audio

## Troubleshooting Audio Issues

### Diagnostic Flow Chart

```
Can't hear anything? 
    ?
Run test tone (--test-audio)
    ?
?? Hear test tone? ? NO ? Fix system audio (see above)
?        ?
?       YES
?        ?
?    Load recording file
?        ?
?    Analyze file
?        ?
?? File has audio data? ? NO ? Recording is silent/empty
?        ?
?       YES  
?        ?
?? Amplitude very low? ? YES ? Boost volume to 150-200%
?        ?
?       NO
?        ?
?    Check debug logs
?        ?
?? Look for processing errors
```

### Common Issues and Solutions

#### **"Can't hear test tone"**
- **Cause**: System audio problem
- **Solution**: Check speakers, Windows settings, restart audio service

#### **"Test tone works but recordings are silent"**  
- **Cause**: Silent/empty recordings or very low volume
- **Solutions**:
  - Analyze file to check for audio data
  - Boost master volume to 150-200%
  - Check if transmissions were actually recorded

#### **"Audio buffer overflow/underflow"**
- **Cause**: Audio system overload
- **Solutions**:
  - Close other audio applications
  - Restart the application
  - Try different audio device

#### **"All audio output methods failed"**
- **Cause**: Severe system audio issues
- **Solutions**:
  - Restart Windows Audio service as administrator
  - Update audio drivers
  - Check for Windows updates
  - Try safe mode testing

### Audio System Information

The enhanced diagnostics provide:
- **Default Audio Device**: Name, status, volume, format
- **All Available Devices**: Complete enumeration
- **Windows Audio Service**: Running status
- **Device Capabilities**: Supported formats and modes
- **Volume Levels**: System and application volumes

### Debug Logging

Enable debug logging to get detailed information about:
- Audio system initialization
- Device enumeration and selection
- Audio processing pipeline
- Volume control effects
- Buffer management
- OPUS decoding status
- Amplitude levels at each stage
- Error conditions and recovery

## Technical Details

### Audio Pipeline
1. **Recording**: UDP packets ? File storage
2. **Reading**: File ? AudioPacketMetadata objects
3. **Processing**: OPUS decoding ? Float samples ? Volume control ? Effects
4. **Conversion**: Float samples ? PCM16 bytes
5. **Output**: Multiple methods (WASAPI/DirectSound/WaveOut) ? Windows audio system

### Audio Output Methods Tested

1. **WASAPI Shared**: Default method, shared with other applications
2. **WASAPI Exclusive**: Exclusive device access, lowest latency
3. **DirectSound**: Legacy DirectX audio, broad compatibility
4. **WaveOut**: Classic Windows audio API, maximum compatibility

### Supported Formats
- **Input**: SRS .raw recording files (custom format)
- **Output**: Standard WAV files (16-bit PCM, 48kHz mono)
- **Audio Codecs**: OPUS and PCM16
- **Test Tones**: Generated at 48kHz, 16-bit, mono

### Performance Notes
- Test tones are generated in real-time (50% amplitude for safety)
- System diagnosis runs quickly (< 1 second)
- File analysis processes the entire recording
- WAV export is limited to first 100 packets (to prevent huge files)
- All audio processing runs on background threads
- Multiple audio methods tested for maximum compatibility