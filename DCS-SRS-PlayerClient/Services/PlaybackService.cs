using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShalevOhad.DCS.SRS.Recorder.Core;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using ShalevOhad.DCS.SRS.Recorder.Core.Debug;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services
{
    /// <summary>
    /// Service implementation for audio playback operations
    /// </summary>
    public class AudioPlaybackService : IAudioPlaybackService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private AudioPacketReader? _reader;
        private PlaybackState _currentState = PlaybackState.Stopped;
        private bool _disposed;

        #region Events

        public event EventHandler? PlaybackStarted;
        public event EventHandler? PlaybackStopped;
        public event EventHandler? PlaybackPaused;
        public event EventHandler? PlaybackResumed;
        public event EventHandler<Exception>? PlaybackError;
        public event EventHandler<PlaybackState>? PlaybackStateChanged;
        public event EventHandler<Core.AudioPacketMetadata>? PacketStarted;

        #endregion

        #region Properties

        public bool IsPlaying => _reader?.IsPlaying ?? false;
        public bool IsPaused => _reader?.IsPaused ?? false;
        public TimeSpan TotalDuration => _reader?.TotalDuration ?? TimeSpan.Zero;
        public TimeSpan CurrentPosition => _reader?.CurrentPosition ?? TimeSpan.Zero;
        public PlaybackState CurrentState => _currentState;

        #endregion

        #region Public Methods

        public async Task StartAsync(string filePath, FrequencyFilterConfig? frequencyFilter = null, AudioConfig? audioConfig = null)
        {
            ThrowIfDisposed();

            if (IsPlaying)
            {
                throw new InvalidOperationException("Playback is already active. Stop current playback first.");
            }

            try
            {
                Logger.Info($"Starting audio playback for: {filePath}");

                // Analyze the file first to help with debugging
                try
                {
                    Logger.Info("Performing pre-playback file analysis...");
                    var analysisResult = await AudioDiagnostics.AnalyzeRecordedFileAsync(filePath);
                    Logger.Info($"File analysis result:\n{analysisResult}");
                    
                    if (analysisResult.PotentialIssues.Any())
                    {
                        Logger.Warn($"Potential audio issues detected: {string.Join(", ", analysisResult.PotentialIssues)}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Could not analyze file before playback, continuing anyway");
                }

                // Create new reader
                _reader = new AudioPacketReader(filePath);
                WireUpReaderEvents();

                // Apply configuration
                ApplyAudioConfig(audioConfig ?? AudioConfig.Default);
                ApplyFrequencyFilter(frequencyFilter ?? FrequencyFilterConfig.Disabled);

                // Start playback
                _reader.StartPlayback();

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to start playback: {filePath}");
                await CleanupReaderAsync();
                throw new InvalidOperationException($"Failed to start playback: {ex.Message}", ex);
            }
        }

        public async Task StopAsync()
        {
            if (!IsPlaying && !IsPaused)
                return;

            try
            {
                Logger.Info("Stopping audio playback");
                
                if (_reader != null)
                {
                    await _reader.StopPlaybackAsync();
                }
                
                await CleanupReaderAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping playback");
                // Don't throw from stop - always try to clean up
                await CleanupReaderAsync();
            }
        }

        public async Task PauseAsync()
        {
            ThrowIfDisposed();

            if (!IsPlaying || IsPaused)
            {
                Logger.Debug("Pause requested but playback is not in a pausable state");
                return;
            }

            try
            {
                Logger.Info("Pausing audio playback");
                _reader?.PausePlayback();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error pausing playback");
                throw new InvalidOperationException($"Failed to pause playback: {ex.Message}", ex);
            }
        }

        public async Task ResumeAsync()
        {
            ThrowIfDisposed();

            if (!IsPaused)
            {
                Logger.Debug("Resume requested but playback is not paused");
                return;
            }

            try
            {
                Logger.Info("Resuming audio playback");
                _reader?.ResumePlayback();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error resuming playback");
                throw new InvalidOperationException($"Failed to resume playback: {ex.Message}", ex);
            }
        }

        public async Task SeekToAsync(TimeSpan position)
        {
            ThrowIfDisposed();
            
            try
            {
                _reader?.SeekTo(position);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error seeking to position: {position}");
                throw new InvalidOperationException($"Failed to seek to position: {ex.Message}", ex);
            }
        }

        public async Task SetVolumeAsync(float volume)
        {
            ThrowIfDisposed();
            
            try
            {
                var clampedVolume = Math.Clamp(volume, 0.0f, 2.0f);
                _reader?.SetMasterVolume(clampedVolume);
                Logger.Debug($"Volume set to: {clampedVolume:F2}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error setting volume: {volume}");
                throw new InvalidOperationException($"Failed to set volume: {ex.Message}", ex);
            }
        }

        public async Task SetFrequencyFilterAsync(FrequencyFilterConfig config)
        {
            ThrowIfDisposed();
            
            try
            {
                ApplyFrequencyFilter(config);
                Logger.Debug($"Frequency filter updated: enabled={config.IsEnabled}, count={config.SelectedFrequencies.Count}");

                // Invoke state change explicitly if the filter is enabled or disabled
                if (config.IsEnabled)
                {
                    PlaybackStateChanged?.Invoke(this, CurrentState);
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error setting frequency filter");
                throw new InvalidOperationException($"Failed to set frequency filter: {ex.Message}", ex);
            }
        }

        public void SetUserSeeking(bool isSeeking)
        {
            ThrowIfDisposed();
            _reader?.SetUserSeeking(isSeeking);
        }

        /// <summary>
        /// Test method to verify audio output is working by playing a test tone
        /// </summary>
        public async Task PlayTestToneAsync(double frequency = 440.0, double durationSeconds = 2.0)
        {
            ThrowIfDisposed();
            
            try
            {
                Logger.Info($"Playing test tone for audio system verification: {frequency}Hz, {durationSeconds}s");
                await AudioDiagnostics.PlayTestToneAsync(frequency, durationSeconds);
                Logger.Info("Test tone completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to play test tone");
                throw new InvalidOperationException($"Test tone playback failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Analyzes a recorded file for potential audio issues
        /// </summary>
        public async Task<string> AnalyzeRecordedFileAsync(string filePath)
        {
            ThrowIfDisposed();
            
            try
            {
                Logger.Info($"Analyzing recorded file: {filePath}");
                var result = await AudioDiagnostics.AnalyzeRecordedFileAsync(filePath);
                Logger.Info($"File analysis completed for: {filePath}");
                return result.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to analyze file: {filePath}");
                throw new InvalidOperationException($"File analysis failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Private Methods

        private void ApplyAudioConfig(AudioConfig config)
        {
            if (_reader != null)
            {
                _reader.SetMasterVolume(config.MasterVolume);
            }
        }

        private void ApplyFrequencyFilter(FrequencyFilterConfig config)
        {
            if (_reader == null) return;

            if (config.IsEnabled && config.SelectedFrequencies.Any())
            {
                _reader.SetFrequencyFilter(config.SelectedFrequencies);
            }
            else
            {
                _reader.ClearFrequencyFilter();
            }
        }

        private void WireUpReaderEvents()
        {
            if (_reader == null) return;

            _reader.PlaybackStarted += OnReaderPlaybackStarted;
            _reader.PlaybackStopped += OnReaderPlaybackStopped;
            _reader.PlaybackPaused += OnReaderPlaybackPaused;
            _reader.PlaybackResumed += OnReaderPlaybackResumed;
            _reader.PlaybackError += OnReaderPlaybackError;
            _reader.PlaybackProgressChanged += OnReaderPlaybackProgressChanged;
            _reader.PacketStarted += OnReaderPacketStarted;
            _reader.PlaybackTimeChanged += OnReaderPlaybackTimeChanged;
        }

        private void UnwireReaderEvents()
        {
            if (_reader == null) return;

            _reader.PlaybackStarted -= OnReaderPlaybackStarted;
            _reader.PlaybackStopped -= OnReaderPlaybackStopped;
            _reader.PlaybackPaused -= OnReaderPlaybackPaused;
            _reader.PlaybackResumed -= OnReaderPlaybackResumed;
            _reader.PlaybackError -= OnReaderPlaybackError;
            _reader.PlaybackProgressChanged -= OnReaderPlaybackProgressChanged;
            _reader.PacketStarted -= OnReaderPacketStarted;
            _reader.PlaybackTimeChanged -= OnReaderPlaybackTimeChanged;
        }

        private async Task CleanupReaderAsync()
        {
            if (_reader != null)
            {
                UnwireReaderEvents();
                _reader.Dispose();
                _reader = null;
            }
            await Task.CompletedTask;
        }

        private void UpdateCurrentState()
        {
            if (_reader == null)
            {
                _currentState = PlaybackState.Stopped;
            }
            else if (_reader.IsPaused)
            {
                _currentState = PlaybackState.Paused(_reader.CurrentPosition, _reader.TotalDuration);
            }
            else if (_reader.IsPlaying)
            {
                _currentState = PlaybackState.Playing(_reader.CurrentPosition, _reader.TotalDuration);
            }
            else
            {
                _currentState = PlaybackState.Stopped;
            }

            PlaybackStateChanged?.Invoke(this, _currentState);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioPlaybackService));
        }

        #endregion

        #region Reader Event Handlers

        private void OnReaderPlaybackStarted()
        {
            UpdateCurrentState();
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
        }

        private void OnReaderPlaybackStopped()
        {
            UpdateCurrentState();
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        private void OnReaderPlaybackPaused()
        {
            UpdateCurrentState();
            PlaybackPaused?.Invoke(this, EventArgs.Empty);
        }

        private void OnReaderPlaybackResumed()
        {
            UpdateCurrentState();
            PlaybackResumed?.Invoke(this, EventArgs.Empty);
        }

        private void OnReaderPlaybackError(Exception ex)
        {
            UpdateCurrentState();
            PlaybackError?.Invoke(this, ex);
        }

        private void OnReaderPlaybackProgressChanged(double percent)
        {
            UpdateCurrentState();
        }

        private void OnReaderPacketStarted(Core.AudioPacketMetadata packet)
        {
            PacketStarted?.Invoke(this, packet);
        }

        private void OnReaderPlaybackTimeChanged(TimeSpan current, TimeSpan total)
        {
            UpdateCurrentState();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            _ = CleanupReaderAsync();
            _disposed = true;

            Logger.Debug("AudioPlaybackService disposed");
        }

        #endregion
    }
}