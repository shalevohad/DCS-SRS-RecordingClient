using System;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Infrastructure
{
    /// <summary>
    /// Factory for creating and configuring the main form and its dependencies
    /// </summary>
    public static class MainFormFactory
    {
        /// <summary>
        /// Creates and configures the main form with all dependencies
        /// </summary>
        public static MainPlayerForm CreateMainForm()
        {
            var form = new MainPlayerForm();
            var serviceContainer = CreateServiceContainer(form);
            
            // Store the service container for later initialization in the Load event
            // This avoids race conditions and ensures UI thread safety
            form.Tag = serviceContainer;
            
            return form;
        }

        /// <summary>
        /// Creates a properly configured service container using the existing configuration
        /// </summary>
        private static ServiceContainer CreateServiceContainer(MainPlayerForm form)
        {
            return ServiceConfiguration.ConfigureServices(form);
        }

        /// <summary>
        /// Creates the main form in a more testable way with dependency injection
        /// </summary>
        public static async System.Threading.Tasks.Task<MainPlayerForm> CreateMainFormForTestingAsync(ServiceContainer serviceContainer)
        {
            var form = new MainPlayerForm();
            await form.InitializeAsync(serviceContainer);
            return form;
        }
    }

    /// <summary>
    /// Extension methods for easier service container usage
    /// </summary>
    public static class ServiceContainerExtensions
    {
        /// <summary>
        /// Registers a service with automatic disposal
        /// </summary>
        public static void RegisterDisposable<TInterface, TImplementation>(
            this ServiceContainer container, 
            TImplementation instance) 
            where TImplementation : class, TInterface, IDisposable
        {
            container.RegisterSingleton<TInterface, TImplementation>(instance);
            
            // Ensure disposal when container is disposed
            if (container is IDisposable disposableContainer)
            {
                // The ServiceContainer should handle disposal of registered services
            }
        }

        /// <summary>
        /// Gets a service with null checking and better error messages
        /// </summary>
        public static T GetRequiredService<T>(this ServiceContainer container)
        {
            if (container.TryGetService<T>(out var service) && service != null)
            {
                return service;
            }

            throw new InvalidOperationException($"Required service of type {typeof(T).Name} is not registered.");
        }
    }
}