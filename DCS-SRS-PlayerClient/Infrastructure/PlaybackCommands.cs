using System;
using System.Threading.Tasks;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Infrastructure
{
    /// <summary>
    /// Command pattern implementation for playback operations
    /// </summary>
    public interface IPlaybackCommand
    {
        Task ExecuteAsync();
        bool CanExecute();
    }

    public class PlayCommand : IPlaybackCommand
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly Func<string> _getCurrentFile;
        private readonly Func<FrequencyFilterConfig> _getFilterConfig;
        private readonly Func<AudioConfig> _getAudioConfig;
        private readonly Func<string, FrequencyFilterConfig, AudioConfig, Task> _playAsync;
        private readonly Action<string> _showWarning;

        public PlayCommand(
            Func<string> getCurrentFile,
            Func<FrequencyFilterConfig> getFilterConfig,
            Func<AudioConfig> getAudioConfig,
            Func<string, FrequencyFilterConfig, AudioConfig, Task> playAsync,
            Action<string> showWarning)
        {
            _getCurrentFile = getCurrentFile ?? throw new ArgumentNullException(nameof(getCurrentFile));
            _getFilterConfig = getFilterConfig ?? throw new ArgumentNullException(nameof(getFilterConfig));
            _getAudioConfig = getAudioConfig ?? throw new ArgumentNullException(nameof(getAudioConfig));
            _playAsync = playAsync ?? throw new ArgumentNullException(nameof(playAsync));
            _showWarning = showWarning ?? throw new ArgumentNullException(nameof(showWarning));
        }

        public bool CanExecute()
        {
            return !string.IsNullOrEmpty(_getCurrentFile());
        }

        public async Task ExecuteAsync()
        {
            var currentFile = _getCurrentFile();
            if (string.IsNullOrEmpty(currentFile))
            {
                _showWarning("Please open a recording file first.");
                return;
            }

            try
            {
                var filterConfig = _getFilterConfig();
                var audioConfig = _getAudioConfig();
                await _playAsync(currentFile, filterConfig, audioConfig);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing play command");
                throw;
            }
        }
    }

    public class PauseCommand : IPlaybackCommand
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly Func<bool> _isPaused;
        private readonly Func<Task> _pauseAsync;
        private readonly Func<Task> _resumeAsync;

        public PauseCommand(
            Func<bool> isPaused,
            Func<Task> pauseAsync,
            Func<Task> resumeAsync)
        {
            _isPaused = isPaused ?? throw new ArgumentNullException(nameof(isPaused));
            _pauseAsync = pauseAsync ?? throw new ArgumentNullException(nameof(pauseAsync));
            _resumeAsync = resumeAsync ?? throw new ArgumentNullException(nameof(resumeAsync));
        }

        public bool CanExecute()
        {
            return true; // Can always attempt to pause/resume
        }

        public async Task ExecuteAsync()
        {
            try
            {
                if (_isPaused())
                {
                    await _resumeAsync();
                }
                else
                {
                    await _pauseAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing pause/resume command");
                throw;
            }
        }
    }

    public class StopCommand : IPlaybackCommand
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly Func<Task> _stopAsync;

        public StopCommand(Func<Task> stopAsync)
        {
            _stopAsync = stopAsync ?? throw new ArgumentNullException(nameof(stopAsync));
        }

        public bool CanExecute()
        {
            return true; // Can always attempt to stop
        }

        public async Task ExecuteAsync()
        {
            try
            {
                await _stopAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing stop command");
                throw;
            }
        }
    }
}