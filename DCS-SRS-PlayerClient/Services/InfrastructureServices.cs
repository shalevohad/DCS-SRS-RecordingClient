using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using ShalevOhad.DCS.SRS.Recorder.Core.Settings;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services
{
    /// <summary>
    /// Service for managing application settings - provides a clean interface to PlayerSettingsStore
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        // Direct access to the singleton - no need for extra instance variable
        private static PlayerSettingsStore Settings => PlayerSettingsStore.Instance;

        public string LastFilePath 
        { 
            get => Settings.GetPlayerSettingString(PlayerSettingKeys.LastRecordingFile);
            set => Settings.SetPlayerSetting(PlayerSettingKeys.LastRecordingFile, value ?? string.Empty);
        }

        public float DefaultVolume 
        { 
            get => Settings.GetPlayerSettingInt(PlayerSettingKeys.MasterVolume) / 100.0f;
            set => Settings.SetPlayerSetting(PlayerSettingKeys.MasterVolume, (int)(value * 100));
        }

        public bool EnableFrequencyFilterByDefault 
        { 
            get => Settings.GetPlayerSettingBool(PlayerSettingKeys.EnableFrequencyFilterByDefault);
            set => Settings.SetPlayerSetting(PlayerSettingKeys.EnableFrequencyFilterByDefault, value);
        }

        // Simplified async methods - PlayerSettingsStore handles persistence automatically
        public Task LoadAsync() => Task.CompletedTask; // Settings are loaded on first access
        public Task SaveAsync() => Task.CompletedTask; // Settings are saved automatically on changes
    }

    /// <summary>
    /// Service for UI operations and dialogs
    /// </summary>
    public class UIService : IUIService
    {
        private readonly System.Windows.Forms.Control _parentControl;

        public UIService(System.Windows.Forms.Control parentControl)
        {
            _parentControl = parentControl ?? throw new ArgumentNullException(nameof(parentControl));
        }

        public void ShowError(string message, string title = "Error")
        {
            InvokeOnUIThread(() =>
                System.Windows.Forms.MessageBox.Show(message, title, 
                    System.Windows.Forms.MessageBoxButtons.OK, 
                    System.Windows.Forms.MessageBoxIcon.Error));
        }

        public void ShowInfo(string message, string title = "Information")
        {
            InvokeOnUIThread(() =>
                System.Windows.Forms.MessageBox.Show(message, title, 
                    System.Windows.Forms.MessageBoxButtons.OK, 
                    System.Windows.Forms.MessageBoxIcon.Information));
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            InvokeOnUIThread(() =>
                System.Windows.Forms.MessageBox.Show(message, title, 
                    System.Windows.Forms.MessageBoxButtons.OK, 
                    System.Windows.Forms.MessageBoxIcon.Warning));
        }

        public async Task<string?> ShowOpenFileDialogAsync(string filter, string title)
        {
            return await Task.Run(() =>
            {
                string? result = null;
                
                InvokeOnUIThread(() =>
                {
                    using var ofd = new System.Windows.Forms.OpenFileDialog
                    {
                        Filter = filter,
                        Title = title,
                        CheckFileExists = true,
                        CheckPathExists = true
                    };

                    if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        result = ofd.FileName;
                    }
                });

                return result;
            });
        }

        public void InvokeOnUIThread(Action action)
        {
            if (_parentControl.InvokeRequired)
                _parentControl.Invoke(action);
            else
                action();
        }
    }

    /// <summary>
    /// Service wrapper for waveform visualization operations
    /// </summary>
    public class WaveformService : IWaveformService
    {
        private readonly Controls.WaveformSeekBar _waveformSeekBar;
        private string? _currentFilePath;

        public WaveformService(Controls.WaveformSeekBar waveformSeekBar)
        {
            _waveformSeekBar = waveformSeekBar ?? throw new ArgumentNullException(nameof(waveformSeekBar));
            
            // Wire up the seek event
            _waveformSeekBar.PositionChanged += OnWaveformPositionChanged;
        }

        public bool IsUserSeeking => _waveformSeekBar.IsDragging;

        public event EventHandler<int>? SeekRequested;

        private void OnWaveformPositionChanged(object? sender, int position)
        {
            SeekRequested?.Invoke(this, position);
        }

        public async Task LoadWaveformAsync(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                _currentFilePath = filePath;
                await _waveformSeekBar.SetWaveformDataAsync(filePath);
            }
        }

        public async Task ApplyFrequencyFilterAsync(FrequencyFilterConfig config)
        {
            if (config.IsEnabled && config.SelectedFrequencies.Any())
            {
                var frequencyModulationTuples = config.SelectedFrequencies
                    .Select(fm => (fm.Frequency * 1_000_000.0, fm.Modulation)) // Convert MHz to Hz
                    .ToList();
                
                _waveformSeekBar.SetFrequencyFilter(frequencyModulationTuples, true);
            }
            else
            {
                _waveformSeekBar.ClearFrequencyFilter();
            }

            // Refresh the waveform if we have a current file path
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                await _waveformSeekBar.RefreshWaveformAsync(_currentFilePath);
            }
        }

        public void ClearWaveform()
        {
            _waveformSeekBar.ClearWaveform();
            _currentFilePath = null;
        }

        public void UpdatePlaybackPosition(int position)
        {
            _waveformSeekBar.Position = position;
        }
    }
}