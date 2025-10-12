using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.ViewModels
{
    /// <summary>
    /// Presenter/ViewModel for the main player form to handle business logic
    /// </summary>
    public class MainPlayerPresenter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IRecordingInfoService _recordingInfoService;
        private readonly IAudioPlaybackService _audioPlaybackService;
        private readonly ISettingsService _settingsService;
        private readonly IUIService _uiService;
        private readonly IWaveformService _waveformService;

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<bool>? LoadingStateChanged;

        public MainPlayerPresenter(
            IRecordingInfoService recordingInfoService,
            IAudioPlaybackService audioPlaybackService,
            ISettingsService settingsService,
            IUIService uiService,
            IWaveformService waveformService)
        {
            _recordingInfoService = recordingInfoService ?? throw new ArgumentNullException(nameof(recordingInfoService));
            _audioPlaybackService = audioPlaybackService ?? throw new ArgumentNullException(nameof(audioPlaybackService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            _waveformService = waveformService ?? throw new ArgumentNullException(nameof(waveformService));

            SetupServiceEventHandlers();
        }

        public async Task InitializeAsync()
        {
            try
            {
                await _settingsService.LoadAsync();
                OnStatusChanged("Initialized");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during initialization");
                _uiService.ShowError($"Error during initialization: {ex.Message}");
            }
        }

        public async Task LoadLastFileAsync()
        {
            try
            {
                var lastFile = _settingsService.LastFilePath;
                if (!string.IsNullOrEmpty(lastFile) && System.IO.File.Exists(lastFile))
                {
                    // This would trigger the PlayerTabView to load the file
                    OnStatusChanged($"Loading last file: {lastFile}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading last file");
            }
        }

        public async Task<string?> ShowOpenFileDialogAsync()
        {
            try
            {
                return await _uiService.ShowOpenFileDialogAsync(
                    "SRS Recording Files (*.raw)|*.raw|All Files (*.*)|*.*",
                    "Open SRS Recording");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing open file dialog");
                _uiService.ShowError($"Error opening file dialog: {ex.Message}");
                return null;
            }
        }

        public async Task ShutdownAsync()
        {
            try
            {
                await _audioPlaybackService.StopAsync();
                await _settingsService.SaveAsync();
                OnStatusChanged("Shutdown complete");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during shutdown");
            }
        }

        private void SetupServiceEventHandlers()
        {
            if (_audioPlaybackService != null)
            {
                _audioPlaybackService.PlaybackStarted += (s, e) => OnStatusChanged("Playing");
                _audioPlaybackService.PlaybackStopped += (s, e) => OnStatusChanged("Stopped");
                _audioPlaybackService.PlaybackPaused += (s, e) => OnStatusChanged("Paused");
                _audioPlaybackService.PlaybackResumed += (s, e) => OnStatusChanged("Playing");
                _audioPlaybackService.PlaybackError += (s, e) => OnStatusChanged("Error");
            }
        }

        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        private void OnLoadingStateChanged(bool isLoading)
        {
            LoadingStateChanged?.Invoke(this, isLoading);
        }
    }
}