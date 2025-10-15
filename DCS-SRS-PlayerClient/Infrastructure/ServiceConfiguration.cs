using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Infrastructure;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Controls;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Infrastructure
{
    /// <summary>
    /// Service configuration and dependency injection for the player client
    /// </summary>
    public static class ServiceConfiguration
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Configures all services with the main form instance
        /// </summary>
        public static ServiceContainer ConfigureServices(MainPlayerForm mainForm)
        {
            var container = new ServiceContainer();

            try
            {
                Logger.Info("Starting service configuration");

                // Register infrastructure services
                RegisterInfrastructureServices(container);

                // Register UI-dependent services
                RegisterUIServices(container, mainForm);

                // Register form-dependent services (services that need the actual form)
                RegisterFormDependentServices(container, mainForm);

                Logger.Info("Service configuration completed successfully");
                return container;
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Failed to configure services");
                throw;
            }
        }

        /// <summary>
        /// Registers basic infrastructure services that don't depend on UI
        /// </summary>
        public static void RegisterInfrastructureServices(ServiceContainer container)
        {
            Logger.Debug("Registering infrastructure services");

            // Settings service
            container.RegisterSingleton<ISettingsService, SettingsService>(new SettingsService());

            // Recording info service
            container.RegisterSingleton<IRecordingInfoService, RecordingInfoService>(new RecordingInfoService());

            // Audio playback service
            container.RegisterSingleton<IAudioPlaybackService, AudioPlaybackService>(new AudioPlaybackService());

            Logger.Debug("Infrastructure services registered");
        }

        /// <summary>
        /// Registers UI services that need a control reference
        /// </summary>
        public static void RegisterUIServices(ServiceContainer container, System.Windows.Forms.Control parentControl)
        {
            Logger.Debug("Registering UI services");

            // UI service for dialogs and UI operations
            container.RegisterSingleton<IUIService, UIService>(new UIService(parentControl));

            Logger.Debug("UI services registered");
        }

        /// <summary>
        /// Registers services that depend on specific form controls
        /// </summary>
        private static void RegisterFormDependentServices(ServiceContainer container, MainPlayerForm mainForm)
        {
            Logger.Debug("Registering form-dependent services");

            // Register a placeholder waveform service initially
            // This will be replaced with the actual service when the waveform control is created
            container.RegisterFactory<IWaveformService>(_ => new PlaceholderWaveformService());

            // Enhanced UI services are now integrated directly into components
            // These services would be implemented in future iterations if needed as separate services
            // For now, the functionality is embedded in the components themselves

            Logger.Debug("Form-dependent services registered");
        }

        /// <summary>
        /// Placeholder waveform service for initialization phase
        /// </summary>
        private class PlaceholderWaveformService : IWaveformService
        {
            public bool IsUserSeeking => false;

            public event EventHandler<int>? SeekRequested;

            public Task ApplyFrequencyFilterAsync(ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models.FrequencyFilterConfig config)
            {
                return Task.CompletedTask;
            }

            public void ClearWaveform()
            {
                // No-op
            }

            public Task LoadWaveformAsync(string filePath)
            {
                return Task.CompletedTask;
            }

            public void UpdatePlaybackPosition(int position)
            {
                // No-op
            }
        }

        /// <summary>
        /// Registers the waveform service after the waveform control is created
        /// </summary>
        public static void RegisterWaveformService(ServiceContainer container, WaveformSeekBar waveformSeekBar)
        {
            Logger.Debug("Registering waveform service");

            if (waveformSeekBar == null)
            {
                throw new ArgumentNullException(nameof(waveformSeekBar));
            }

            container.RegisterSingleton<IWaveformService, WaveformService>(new WaveformService(waveformSeekBar));

            Logger.Debug("Waveform service registered");
        }
    }
}