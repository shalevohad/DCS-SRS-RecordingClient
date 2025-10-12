using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Controls;
using ShalevOhad.DCS.SRS.Recorder.Core.Debug;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using NAudio.CoreAudioApi;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Tabs
{
    /// <summary>
    /// Tab control for audio testing functionality
    /// </summary>
    public partial class AudioTestTabView : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Services
        private IAudioPlaybackService? _audioPlaybackService;
        private IUIService? _uiService;

        // Controls
        private Panel _audioTestPanel;
        private ComboBox _frequencyComboBox;
        private NumericUpDown _durationNumericUpDown;
        private Button _playTestToneButton;
        private Button _stopTestToneButton;
        private VolumeControl _testVolumeControl;
        private Label _audioTestStatusLabel;
        private RichTextBox _audioTestResultsTextBox;
        private ComboBox _audioDeviceComboBox;
        private Button _refreshDevicesButton;
        private Button _runBasicTestButton;
        private Button _runAdvancedTestButton;
        private Button _systemDiagnosticsButton;

        private List<AudioDeviceInfo> _availableAudioDevices = new();

        public event EventHandler<string>? StatusChanged;

        public AudioTestTabView()
        {
            InitializeComponent();
            CreateControls();
            SetupEventHandlers();
        }

        public void Initialize(IAudioPlaybackService audioPlaybackService, IUIService uiService)
        {
            _audioPlaybackService = audioPlaybackService ?? throw new ArgumentNullException(nameof(audioPlaybackService));
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
        }

        public async Task InitializeAudioDevicesAsync()
        {
            try
            {
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] Detecting audio devices...");

                using var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                _availableAudioDevices.Clear();
                _audioDeviceComboBox.Items.Clear();

                // Add default device first
                try
                {
                    var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    var defaultDeviceInfo = new AudioDeviceInfo
                    {
                        FriendlyName = $"{defaultDevice.FriendlyName} (Default)",
                        DeviceId = defaultDevice.ID,
                        IsEnabled = defaultDevice.State == DeviceState.Active,
                        MixFormat = defaultDevice.AudioClient?.MixFormat?.ToString() ?? "Unknown",
                        Volume = defaultDevice.AudioEndpointVolume?.MasterVolumeLevelScalar ?? 0.0f
                    };

                    _availableAudioDevices.Add(defaultDeviceInfo);
                    _audioDeviceComboBox.Items.Add(defaultDeviceInfo.FriendlyName);

                    AppendToResults($"[{DateTime.Now:HH:mm:ss}] Found default device: {defaultDevice.FriendlyName}");
                }
                catch (Exception ex)
                {
                    AppendToResults($"[{DateTime.Now:HH:mm:ss}] Warning: Could not get default device: {ex.Message}");
                }

                // Add all other active devices
                foreach (var device in devices)
                {
                    try
                    {
                        // Skip if this is already the default device
                        if (_availableAudioDevices.Any(d => d.DeviceId == device.ID))
                            continue;

                        var deviceInfo = new AudioDeviceInfo
                        {
                            FriendlyName = device.FriendlyName,
                            DeviceId = device.ID,
                            IsEnabled = device.State == DeviceState.Active,
                            MixFormat = device.AudioClient?.MixFormat?.ToString() ?? "Unknown",
                            Volume = device.AudioEndpointVolume?.MasterVolumeLevelScalar ?? 0.0f
                        };

                        _availableAudioDevices.Add(deviceInfo);
                        _audioDeviceComboBox.Items.Add(deviceInfo.FriendlyName);
                    }
                    catch (Exception ex)
                    {
                        AppendToResults($"[{DateTime.Now:HH:mm:ss}] Warning: Could not access device {device.FriendlyName}: {ex.Message}");
                    }
                }

                if (_audioDeviceComboBox.Items.Count > 0)
                {
                    _audioDeviceComboBox.SelectedIndex = 0; // Select default device
                    AppendToResults($"[{DateTime.Now:HH:mm:ss}] Found {_availableAudioDevices.Count} audio devices");
                }
                else
                {
                    AppendToResults($"[{DateTime.Now:HH:mm:ss}] ERROR: No audio devices found!");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing audio devices");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] ERROR initializing audio devices: {ex.Message}");
            }
        }

        private void CreateControls()
        {
            _audioTestPanel = new Panel();
            _audioTestPanel.Dock = DockStyle.Fill;
            _audioTestPanel.Padding = new Padding(12);
            Controls.Add(_audioTestPanel);

            CreateDeviceSelectionPanel();
            CreateTestControlsPanel();
            CreateResultsTextBox();
        }

        private void CreateDeviceSelectionPanel()
        {
            var deviceSelectionPanel = new Panel();
            deviceSelectionPanel.Height = 50;
            deviceSelectionPanel.Dock = DockStyle.Top;
            deviceSelectionPanel.BorderStyle = BorderStyle.FixedSingle;
            deviceSelectionPanel.Padding = new Padding(12);

            var deviceTableLayout = new TableLayoutPanel();
            deviceTableLayout.Dock = DockStyle.Fill;
            deviceTableLayout.ColumnCount = 3;
            deviceTableLayout.RowCount = 1;
            deviceTableLayout.Margin = new Padding(0);
            deviceTableLayout.Padding = new Padding(0);

            deviceTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            deviceTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            deviceTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            deviceTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            deviceSelectionPanel.Controls.Add(deviceTableLayout);

            var deviceLabel = new Label();
            deviceLabel.Text = "Audio Device:";
            deviceLabel.AutoSize = true;
            deviceLabel.Anchor = AnchorStyles.Left;
            deviceLabel.TextAlign = ContentAlignment.MiddleLeft;
            deviceLabel.Margin = new Padding(0, 0, 12, 0);
            deviceTableLayout.Controls.Add(deviceLabel, 0, 0);

            _audioDeviceComboBox = new ComboBox();
            _audioDeviceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _audioDeviceComboBox.Dock = DockStyle.Fill;
            _audioDeviceComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            _audioDeviceComboBox.Margin = new Padding(0, 0, 12, 0);
            deviceTableLayout.Controls.Add(_audioDeviceComboBox, 1, 0);

            _refreshDevicesButton = new Button();
            _refreshDevicesButton.Text = "Refresh";
            _refreshDevicesButton.Size = new Size(80, 28);
            _refreshDevicesButton.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            _refreshDevicesButton.Margin = new Padding(0, 0, 0, 0);
            deviceTableLayout.Controls.Add(_refreshDevicesButton, 2, 0);

            _audioTestPanel.Controls.Add(deviceSelectionPanel);
        }

        private void CreateTestControlsPanel()
        {
            var testControlsPanel = new Panel();
            testControlsPanel.Height = 160;
            testControlsPanel.Dock = DockStyle.Top;
            testControlsPanel.BorderStyle = BorderStyle.FixedSingle;
            testControlsPanel.Padding = new Padding(12);

            // Create a more organized layout with grouped panels
            var topRowPanel = new Panel();
            topRowPanel.Height = 40;
            topRowPanel.Dock = DockStyle.Top;
            
            var middleRowPanel = new Panel();
            middleRowPanel.Height = 40;
            middleRowPanel.Dock = DockStyle.Top;
            
            var bottomRowPanel = new Panel();
            bottomRowPanel.Height = 40;
            bottomRowPanel.Dock = DockStyle.Top;
            
            var statusPanel = new Panel();
            statusPanel.Dock = DockStyle.Fill;

            testControlsPanel.Controls.Add(statusPanel);     // Fill first (bottom in dock order)
            testControlsPanel.Controls.Add(bottomRowPanel);  // Top third
            testControlsPanel.Controls.Add(middleRowPanel);  // Top second  
            testControlsPanel.Controls.Add(topRowPanel);     // Top first

            CreateFrequencyAndDurationControls(topRowPanel);
            CreateVolumeAndTestControls(middleRowPanel);
            CreatePlaybackAndAdvancedControls(bottomRowPanel);
            CreateStatusLabel(statusPanel);

            _audioTestPanel.Controls.Add(testControlsPanel);
        }

        private void CreateFrequencyAndDurationControls(Panel panel)
        {
            var flowLayout = new FlowLayoutPanel();
            flowLayout.Dock = DockStyle.Fill;
            flowLayout.FlowDirection = FlowDirection.LeftToRight;
            flowLayout.WrapContents = false;
            flowLayout.AutoSize = false;
            
            panel.Controls.Add(flowLayout);

            var freqLabel = new Label();
            freqLabel.Text = "Test Frequency (Hz):";
            freqLabel.AutoSize = true;
            freqLabel.TextAlign = ContentAlignment.MiddleLeft;
            freqLabel.Margin = new Padding(0, 8, 8, 0);
            flowLayout.Controls.Add(freqLabel);

            _frequencyComboBox = new ComboBox();
            _frequencyComboBox.Items.AddRange(new object[] { "220", "440", "880", "1000", "1500", "2000" });
            _frequencyComboBox.SelectedIndex = 1; // Default to 440Hz
            _frequencyComboBox.Width = 80;
            _frequencyComboBox.Margin = new Padding(0, 6, 16, 0);
            flowLayout.Controls.Add(_frequencyComboBox);

            var durationLabel = new Label();
            durationLabel.Text = "Duration (seconds):";
            durationLabel.AutoSize = true;
            durationLabel.TextAlign = ContentAlignment.MiddleLeft;
            durationLabel.Margin = new Padding(0, 8, 8, 0);
            flowLayout.Controls.Add(durationLabel);

            _durationNumericUpDown = new NumericUpDown();
            _durationNumericUpDown.Minimum = 1;
            _durationNumericUpDown.Maximum = 10;
            _durationNumericUpDown.Value = 2;
            _durationNumericUpDown.DecimalPlaces = 1;
            _durationNumericUpDown.Increment = 0.5m;
            _durationNumericUpDown.Width = 70;
            _durationNumericUpDown.Margin = new Padding(0, 6, 0, 0);
            flowLayout.Controls.Add(_durationNumericUpDown);
        }

        private void CreateVolumeAndTestControls(Panel panel)
        {
            var flowLayout = new FlowLayoutPanel();
            flowLayout.Dock = DockStyle.Fill;
            flowLayout.FlowDirection = FlowDirection.LeftToRight;
            flowLayout.WrapContents = false;
            flowLayout.AutoSize = false;
            
            panel.Controls.Add(flowLayout);

            var volumeLabel = new Label();
            volumeLabel.Text = "Test Volume:";
            volumeLabel.AutoSize = true;
            volumeLabel.TextAlign = ContentAlignment.MiddleLeft;
            volumeLabel.Margin = new Padding(0, 8, 8, 0);
            flowLayout.Controls.Add(volumeLabel);

            _testVolumeControl = new VolumeControl();
            _testVolumeControl.Size = new Size(120, 30);
            _testVolumeControl.Margin = new Padding(0, 2, 32, 0);
            flowLayout.Controls.Add(_testVolumeControl);

            _runBasicTestButton = new Button();
            _runBasicTestButton.Text = "Basic Audio Test";
            _runBasicTestButton.Size = new Size(130, 32);
            _runBasicTestButton.Margin = new Padding(0, 0, 0, 0);
            flowLayout.Controls.Add(_runBasicTestButton);
        }

        private void CreatePlaybackAndAdvancedControls(Panel panel)
        {
            var flowLayout = new FlowLayoutPanel();
            flowLayout.Dock = DockStyle.Fill;
            flowLayout.FlowDirection = FlowDirection.LeftToRight;
            flowLayout.WrapContents = false;
            flowLayout.AutoSize = false;
            
            panel.Controls.Add(flowLayout);

            _playTestToneButton = new Button();
            _playTestToneButton.Text = "Play Test Tone";
            _playTestToneButton.Size = new Size(110, 32);
            _playTestToneButton.Margin = new Padding(0, 0, 8, 0);
            flowLayout.Controls.Add(_playTestToneButton);

            _stopTestToneButton = new Button();
            _stopTestToneButton.Text = "Stop Test";
            _stopTestToneButton.Size = new Size(80, 32);
            _stopTestToneButton.Enabled = false;
            _stopTestToneButton.Margin = new Padding(0, 0, 16, 0);
            flowLayout.Controls.Add(_stopTestToneButton);

            _runAdvancedTestButton = new Button();
            _runAdvancedTestButton.Text = "Advanced Test";
            _runAdvancedTestButton.Size = new Size(130, 32);
            _runAdvancedTestButton.Margin = new Padding(0, 0, 8, 0);
            flowLayout.Controls.Add(_runAdvancedTestButton);

            _systemDiagnosticsButton = new Button();
            _systemDiagnosticsButton.Text = "System Diagnostics";
            _systemDiagnosticsButton.Size = new Size(140, 32);
            _systemDiagnosticsButton.Margin = new Padding(0, 0, 0, 0);
            flowLayout.Controls.Add(_systemDiagnosticsButton);
        }

        private void CreateStatusLabel(Panel panel)
        {
            _audioTestStatusLabel = new Label();
            _audioTestStatusLabel.Text = "Ready to test audio output";
            _audioTestStatusLabel.Dock = DockStyle.Fill;
            _audioTestStatusLabel.ForeColor = SystemColors.ControlDarkDark;
            _audioTestStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _audioTestStatusLabel.Padding = new Padding(0, 8, 0, 0);
            panel.Controls.Add(_audioTestStatusLabel);
        }

        private void CreateResultsTextBox()
        {
            _audioTestResultsTextBox = new RichTextBox();
            _audioTestResultsTextBox.Dock = DockStyle.Fill;
            _audioTestResultsTextBox.ReadOnly = true;
            _audioTestResultsTextBox.Font = new Font("Consolas", 9F);
            _audioTestResultsTextBox.Text = GetInitialHelpText();
            _audioTestPanel.Controls.Add(_audioTestResultsTextBox);
        }

        private string GetInitialHelpText()
        {
            return "Audio Test Results will appear here...\n\n" +
                   "This comprehensive audio testing tool allows you to:\n" +
                   "• Select and test different audio output devices\n" +
                   "• Run basic Windows audio tests (beeps and system sounds)\n" +
                   "• Perform advanced audio engine tests with various methods\n" +
                   "• Get detailed system diagnostics and recommendations\n" +
                   "• Test custom frequency tones at different volumes\n\n" +
                   "Start by selecting an audio device above, then choose a test type:\n" +
                   "- 'Basic Audio Test': Tests fundamental Windows audio functions\n" +
                   "- 'Advanced Test': Tests all available audio engines and methods\n" +
                   "- 'System Diagnostics': Analyzes your audio system configuration\n" +
                   "- 'Play Test Tone': Plays a simple tone using the current playback service";
        }

        private void SetupEventHandlers()
        {
            _playTestToneButton.Click += OnPlayTestTone;
            _stopTestToneButton.Click += OnStopTestTone;
            _refreshDevicesButton.Click += OnRefreshAudioDevices;
            _runBasicTestButton.Click += OnRunBasicAudioTest;
            _runAdvancedTestButton.Click += OnRunAdvancedAudioTest;
            _systemDiagnosticsButton.Click += OnRunSystemDiagnostics;
            _audioDeviceComboBox.SelectedIndexChanged += OnAudioDeviceChanged;
        }

        #region Event Handlers

        private async void OnPlayTestTone(object? sender, EventArgs e)
        {
            try
            {
                _playTestToneButton.Enabled = false;
                _stopTestToneButton.Enabled = true;
                _audioTestStatusLabel.Text = "Playing test tone...";
                _audioTestStatusLabel.ForeColor = Color.Green;

                if (double.TryParse(_frequencyComboBox.Text, out var frequency))
                {
                    var duration = (double)_durationNumericUpDown.Value;

                    string deviceInfo = "default device";
                    if (_audioDeviceComboBox.SelectedIndex >= 0 && _audioDeviceComboBox.SelectedIndex < _availableAudioDevices.Count)
                    {
                        deviceInfo = _availableAudioDevices[_audioDeviceComboBox.SelectedIndex].FriendlyName;
                    }

                    AppendToResults($"[{DateTime.Now:HH:mm:ss}] Starting test tone: {frequency}Hz for {duration}s");
                    AppendToResults($"[{DateTime.Now:HH:mm:ss}] Using device: {deviceInfo}");
                    AppendToResults($"[{DateTime.Now:HH:mm:ss}] Volume: {_testVolumeControl.CurrentVolume:P0}");

                    if (_audioPlaybackService != null)
                    {
                        await _audioPlaybackService.SetVolumeAsync(_testVolumeControl.CurrentVolume);
                        await _audioPlaybackService.PlayTestToneAsync(frequency, duration);

                        AppendToResults($"[{DateTime.Now:HH:mm:ss}] Test tone completed successfully using playback service");
                    }
                    else
                    {
                        AppendToResults($"[{DateTime.Now:HH:mm:ss}] Playback service not available, using fallback method...");
                        await AudioDiagnostics.PlayTestToneAsync(frequency, duration);
                        AppendToResults($"[{DateTime.Now:HH:mm:ss}] Fallback test tone completed");
                    }

                    _audioTestStatusLabel.Text = "Test completed";
                    _audioTestStatusLabel.ForeColor = SystemColors.ControlDarkDark;
                }
                else
                {
                    AppendToResults($"[{DateTime.Now:HH:mm:ss}] ERROR: Invalid frequency value");
                    _audioTestStatusLabel.Text = "Invalid frequency";
                    _audioTestStatusLabel.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error playing test tone");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] Tip: Try the 'Basic Audio Test' or 'Advanced Test' buttons for more comprehensive testing");
                _audioTestStatusLabel.Text = "Test failed";
                _audioTestStatusLabel.ForeColor = Color.Red;
            }
            finally
            {
                _playTestToneButton.Enabled = true;
                _stopTestToneButton.Enabled = false;
            }
        }

        private async void OnStopTestTone(object? sender, EventArgs e)
        {
            try
            {
                await _audioPlaybackService?.StopAsync()!;
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] Test tone stopped");
                _audioTestStatusLabel.Text = "Test stopped";
                _audioTestStatusLabel.ForeColor = SystemColors.ControlDarkDark;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping test tone");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] ERROR stopping test: {ex.Message}");
            }
            finally
            {
                _playTestToneButton.Enabled = true;
                _stopTestToneButton.Enabled = false;
            }
        }

        private async void OnRefreshAudioDevices(object? sender, EventArgs e)
        {
            try
            {
                _refreshDevicesButton.Enabled = false;
                _audioTestStatusLabel.Text = "Refreshing audio devices...";
                _audioTestStatusLabel.ForeColor = Color.Blue;

                await InitializeAudioDevicesAsync();

                _audioTestStatusLabel.Text = "Audio devices refreshed";
                _audioTestStatusLabel.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error refreshing audio devices");
                _audioTestStatusLabel.Text = "Failed to refresh devices";
                _audioTestStatusLabel.ForeColor = Color.Red;
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] ERROR refreshing devices: {ex.Message}");
            }
            finally
            {
                _refreshDevicesButton.Enabled = true;
            }
        }

        private async void OnRunBasicAudioTest(object? sender, EventArgs e)
        {
            try
            {
                _runBasicTestButton.Enabled = false;
                _audioTestStatusLabel.Text = "Running basic audio test...";
                _audioTestStatusLabel.ForeColor = Color.Blue;

                AppendToResults($"\n[{DateTime.Now:HH:mm:ss}] ===== STARTING BASIC AUDIO TEST =====");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] This test will play various beeps and system sounds");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] Listen carefully and note what you hear:");

                await BasicAudioTest.TestBasicWindowsAudioAsync();

                AppendToResults($"[{DateTime.Now:HH:mm:ss}] ===== BASIC AUDIO TEST COMPLETED =====");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] If you heard beeps, your basic Windows audio is working.");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] If you heard nothing, check your speakers and Windows audio settings.");

                _audioTestStatusLabel.Text = "Basic audio test completed";
                _audioTestStatusLabel.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error running basic audio test");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] ERROR: Basic audio test failed: {ex.Message}");
                _audioTestStatusLabel.Text = "Basic test failed";
                _audioTestStatusLabel.ForeColor = Color.Red;
            }
            finally
            {
                _runBasicTestButton.Enabled = true;
            }
        }

        private async void OnRunAdvancedAudioTest(object? sender, EventArgs e)
        {
            try
            {
                _runAdvancedTestButton.Enabled = false;
                _audioTestStatusLabel.Text = "Running advanced audio test...";
                _audioTestStatusLabel.ForeColor = Color.Blue;

                var frequency = 440.0;
                var duration = 2.0;

                if (double.TryParse(_frequencyComboBox.Text, out var freq))
                    frequency = freq;

                duration = (double)_durationNumericUpDown.Value;

                AppendToResults($"\n[{DateTime.Now:HH:mm:ss}] ===== STARTING ADVANCED AUDIO TEST =====");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] Testing frequency: {frequency}Hz for {duration} seconds");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] This comprehensive test will try multiple audio methods");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] You should hear both beeps AND musical tones:");

                await AudioDiagnostics.PlayTestToneAsync(frequency, duration);

                AppendToResults($"[{DateTime.Now:HH:mm:ss}] ===== ADVANCED AUDIO TEST COMPLETED =====");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] Check the detailed results above for specific recommendations.");

                _audioTestStatusLabel.Text = "Advanced audio test completed";
                _audioTestStatusLabel.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error running advanced audio test");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] ERROR: Advanced audio test failed: {ex.Message}");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] However, if you heard beeps during the test, your audio hardware works.");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] The failure indicates audio driver compatibility issues.");
                _audioTestStatusLabel.Text = "Advanced test failed";
                _audioTestStatusLabel.ForeColor = Color.Red;
            }
            finally
            {
                _runAdvancedTestButton.Enabled = true;
            }
        }

        private async void OnRunSystemDiagnostics(object? sender, EventArgs e)
        {
            try
            {
                _systemDiagnosticsButton.Enabled = false;
                _audioTestStatusLabel.Text = "Running system diagnostics...";
                _audioTestStatusLabel.ForeColor = Color.Blue;

                AppendToResults($"\n[{DateTime.Now:HH:mm:ss}] ===== STARTING SYSTEM DIAGNOSTICS =====");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] Analyzing your audio system configuration...");

                var systemInfo = await AudioDiagnostics.DiagnoseAudioSystemAsync();

                AppendToResults($"[{DateTime.Now:HH:mm:ss}] === SYSTEM DIAGNOSTICS RESULTS ===");
                AppendToResults($"Windows Audio Service: {(systemInfo.WindowsAudioServiceRunning ? "RUNNING" : "NOT RUNNING")}");

                if (systemInfo.DefaultDevice != null)
                {
                    AppendToResults($"Default Device: {systemInfo.DefaultDevice.FriendlyName}");
                    AppendToResults($"  Volume: {systemInfo.DefaultDevice.Volume:P0}");
                    AppendToResults($"  Enabled: {systemInfo.DefaultDevice.IsEnabled}");
                    AppendToResults($"  Format: {systemInfo.DefaultDevice.MixFormat}");
                }
                else
                {
                    AppendToResults($"Default Device: NONE FOUND");
                }

                AppendToResults($"Total Active Devices: {systemInfo.AvailableDevices.Count}");

                foreach (var device in systemInfo.AvailableDevices.Take(5))
                {
                    AppendToResults($"  • {device.FriendlyName} (Volume: {device.Volume:P0}, Enabled: {device.IsEnabled})");
                }

                if (systemInfo.AvailableDevices.Count > 5)
                {
                    AppendToResults($"  ... and {systemInfo.AvailableDevices.Count - 5} more devices");
                }

                if (!string.IsNullOrEmpty(systemInfo.ErrorMessage))
                {
                    AppendToResults($"ERROR: {systemInfo.ErrorMessage}");
                }

                DisplayRecommendations(systemInfo);

                AppendToResults($"[{DateTime.Now:HH:mm:ss}] ===== SYSTEM DIAGNOSTICS COMPLETED =====");

                _audioTestStatusLabel.Text = "System diagnostics completed";
                _audioTestStatusLabel.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error running system diagnostics");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] ERROR: System diagnostics failed: {ex.Message}");
                _audioTestStatusLabel.Text = "System diagnostics failed";
                _audioTestStatusLabel.ForeColor = Color.Red;
            }
            finally
            {
                _systemDiagnosticsButton.Enabled = true;
            }
        }

        private void OnAudioDeviceChanged(object? sender, EventArgs e)
        {
            if (_audioDeviceComboBox.SelectedIndex >= 0 && _audioDeviceComboBox.SelectedIndex < _availableAudioDevices.Count)
            {
                var selectedDevice = _availableAudioDevices[_audioDeviceComboBox.SelectedIndex];
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] Selected audio device: {selectedDevice.FriendlyName}");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] Device format: {selectedDevice.MixFormat}");
                AppendToResults($"[{DateTime.Now:HH:mm:ss}] Device volume: {selectedDevice.Volume:P0}");

                _audioTestStatusLabel.Text = $"Selected: {selectedDevice.FriendlyName}";
                _audioTestStatusLabel.ForeColor = SystemColors.ControlDarkDark;
            }
        }

        #endregion

        #region Helper Methods

        private void AppendToResults(string message)
        {
            _uiService?.InvokeOnUIThread(() =>
            {
                _audioTestResultsTextBox.AppendText(message + Environment.NewLine);
                _audioTestResultsTextBox.ScrollToCaret();
            });
        }

        private void DisplayRecommendations(dynamic systemInfo)
        {
            AppendToResults($"\n[{DateTime.Now:HH:mm:ss}] === RECOMMENDATIONS ===");

            if (!systemInfo.WindowsAudioServiceRunning)
            {
                AppendToResults($"? CRITICAL: Windows Audio service is not running!");
                AppendToResults($"   Fix: Run 'services.msc' and start the Windows Audio service");
            }
            else if (systemInfo.DefaultDevice?.Volume < 0.1f)
            {
                AppendToResults($"??  WARNING: System volume is very low ({systemInfo.DefaultDevice.Volume:P0})");
                AppendToResults($"   Fix: Increase system volume in Windows audio settings");
            }
            else if (!systemInfo.DefaultDevice?.IsEnabled == true)
            {
                AppendToResults($"? CRITICAL: Default audio device is disabled");
                AppendToResults($"   Fix: Enable the default audio device in Windows settings");
            }
            else if (systemInfo.AvailableDevices.Count == 0)
            {
                AppendToResults($"? CRITICAL: No active audio devices found");
                AppendToResults($"   Fix: Check audio drivers and hardware connections");
            }
            else
            {
                AppendToResults($"? Your audio system configuration looks good!");
                AppendToResults($"   If you're having audio issues, try the Advanced Audio Test");
            }
        }

        #endregion

        private void InitializeComponent()
        {
            SuspendLayout();

            Name = "AudioTestTabView";
            Size = new Size(800, 500);

            ResumeLayout(false);
        }
    }

    /// <summary>
    /// Audio device information for device selection
    /// </summary>
    public class AudioDeviceInfo
    {
        public string FriendlyName { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string MixFormat { get; set; } = string.Empty;
        public float Volume { get; set; }
    }
}