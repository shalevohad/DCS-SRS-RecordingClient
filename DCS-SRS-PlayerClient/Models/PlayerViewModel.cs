using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models
{
    /// <summary>
    /// View model for the main player interface
    /// </summary>
    public class PlayerViewModel : INotifyPropertyChanged
    {
        #region Private Fields

        private string _filePath = string.Empty;
        private string _statusText = "Ready";
        private System.Drawing.Color _statusColor = System.Drawing.SystemColors.ControlText;
        private string _infoText = string.Empty;
        private string _currentPacketText = string.Empty;
        private string _currentPacketTooltip = string.Empty;
        private string _currentTime = "0:00";
        private string _totalTime = "0:00";
        private int _progressValue;
        private int _progressMaximum = 100;
        private int _seekPosition;
        private int _seekMaximum = 100;
        private bool _isSeekEnabled;
        private bool _isPlayEnabled = true;
        private bool _isPauseEnabled;
        private bool _isStopEnabled;
        private bool _isBrowseEnabled = true;
        private bool _isUserSeeking;
        private float _volume = 1.0f;
        private bool _isFrequencyFilterEnabled;
        private bool _isPlaying;
        private bool _isPaused;
        private List<FrequencyModulationInfo> _availableFrequencies = new();
        private List<FrequencyModulationInfo> _selectedFrequencies = new();

        #endregion

        #region Public Properties

        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public System.Drawing.Color StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public string InfoText
        {
            get => _infoText;
            set => SetProperty(ref _infoText, value);
        }

        public string CurrentPacketText
        {
            get => _currentPacketText;
            set => SetProperty(ref _currentPacketText, value);
        }

        public string CurrentPacketTooltip
        {
            get => _currentPacketTooltip;
            set => SetProperty(ref _currentPacketTooltip, value);
        }

        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        public string TotalTime
        {
            get => _totalTime;
            set => SetProperty(ref _totalTime, value);
        }

        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, Math.Clamp(value, 0, ProgressMaximum));
        }

        public int ProgressMaximum
        {
            get => _progressMaximum;
            set => SetProperty(ref _progressMaximum, Math.Max(1, value));
        }

        public int SeekPosition
        {
            get => _seekPosition;
            set => SetProperty(ref _seekPosition, Math.Clamp(value, 0, SeekMaximum));
        }

        public int SeekMaximum
        {
            get => _seekMaximum;
            set => SetProperty(ref _seekMaximum, Math.Max(1, value));
        }

        public bool IsSeekEnabled
        {
            get => _isSeekEnabled;
            set => SetProperty(ref _isSeekEnabled, value);
        }

        public bool IsPlayEnabled
        {
            get => _isPlayEnabled;
            set => SetProperty(ref _isPlayEnabled, value);
        }

        public bool IsPauseEnabled
        {
            get => _isPauseEnabled;
            set => SetProperty(ref _isPauseEnabled, value);
        }

        public bool IsStopEnabled
        {
            get => _isStopEnabled;
            set => SetProperty(ref _isStopEnabled, value);
        }

        public bool IsBrowseEnabled
        {
            get => _isBrowseEnabled;
            set => SetProperty(ref _isBrowseEnabled, value);
        }

        public bool IsUserSeeking
        {
            get => _isUserSeeking;
            set => SetProperty(ref _isUserSeeking, value);
        }

        public float Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, Math.Clamp(value, 0.0f, 2.0f));
        }

        public bool IsFrequencyFilterEnabled
        {
            get => _isFrequencyFilterEnabled;
            set => SetProperty(ref _isFrequencyFilterEnabled, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            set => SetProperty(ref _isPaused, value);
        }

        public List<FrequencyModulationInfo> AvailableFrequencies
        {
            get => _availableFrequencies;
            set => SetProperty(ref _availableFrequencies, value ?? new());
        }

        public List<FrequencyModulationInfo> SelectedFrequencies
        {
            get => _selectedFrequencies;
            set => SetProperty(ref _selectedFrequencies, value ?? new());
        }

        #endregion

        #region Computed Properties

        public bool CanPlay => IsPlayEnabled && !string.IsNullOrEmpty(FilePath) && !IsPlaying;
        public bool CanPause => IsPauseEnabled && IsPlaying;
        public bool CanStop => IsStopEnabled && (IsPlaying || IsPaused);
        public bool CanSeek => IsSeekEnabled && !string.IsNullOrEmpty(FilePath);

        #endregion

        #region State Management

        public void UpdatePlaybackState(bool isPlaying, bool isPaused)
        {
            IsPlaying = isPlaying;
            IsPaused = isPaused;
            
            IsPlayEnabled = !isPlaying;
            IsPauseEnabled = isPlaying;
            IsStopEnabled = isPlaying || isPaused;
            
            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(CanPause));
            OnPropertyChanged(nameof(CanStop));
        }

        public void UpdateProgressState(int currentValue, int maximum)
        {
            ProgressMaximum = maximum;
            ProgressValue = currentValue;
        }

        public void UpdateSeekState(int position, int maximum)
        {
            SeekMaximum = maximum;
            SeekPosition = position;
        }

        public void ResetToDefault()
        {
            StatusText = "Ready";
            StatusColor = System.Drawing.SystemColors.ControlText;
            InfoText = string.Empty;
            CurrentPacketText = string.Empty;
            CurrentPacketTooltip = string.Empty;
            CurrentTime = "0:00";
            TotalTime = "0:00";
            ProgressValue = 0;
            SeekPosition = 0;
            IsSeekEnabled = false;
            UpdatePlaybackState(false, false);
            IsBrowseEnabled = true;
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}