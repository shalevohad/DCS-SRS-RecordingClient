using System;
using System.Collections.Generic;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Infrastructure
{
    /// <summary>
    /// Simple dependency injection container for the application
    /// </summary>
    public class ServiceContainer : IDisposable
    {
        private readonly Dictionary<Type, object> _services = new();
        private readonly Dictionary<Type, Func<ServiceContainer, object>> _factories = new();
        private bool _disposed;

        /// <summary>
        /// Registers a singleton service instance
        /// </summary>
        public void RegisterSingleton<TInterface, TImplementation>(TImplementation instance) 
            where TImplementation : class, TInterface
        {
            ThrowIfDisposed();
            _services[typeof(TInterface)] = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        /// <summary>
        /// Registers a service factory
        /// </summary>
        public void RegisterFactory<TInterface>(Func<ServiceContainer, TInterface> factory)
        {
            ThrowIfDisposed();
            _factories[typeof(TInterface)] = container => factory(container) ?? throw new InvalidOperationException($"Factory for {typeof(TInterface).Name} returned null");
        }

        /// <summary>
        /// Registers a transient service (new instance each time)
        /// </summary>
        public void RegisterTransient<TInterface, TImplementation>()
            where TImplementation : class, TInterface, new()
        {
            ThrowIfDisposed();
            _factories[typeof(TInterface)] = _ => new TImplementation();
        }

        /// <summary>
        /// Gets a service of the specified type
        /// </summary>
        public T GetService<T>()
        {
            ThrowIfDisposed();
            
            var serviceType = typeof(T);
            
            // Check for registered singleton
            if (_services.TryGetValue(serviceType, out var service))
            {
                return (T)service;
            }
            
            // Check for factory
            if (_factories.TryGetValue(serviceType, out var factory))
            {
                var instance = factory(this);
                return (T)instance;
            }
            
            throw new InvalidOperationException($"Service of type {serviceType.Name} is not registered");
        }

        /// <summary>
        /// Tries to get a service of the specified type
        /// </summary>
        public bool TryGetService<T>(out T? service)
        {
            try
            {
                service = GetService<T>();
                return true;
            }
            catch
            {
                service = default;
                return false;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServiceContainer));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            // Dispose all registered singletons that implement IDisposable
            foreach (var service in _services.Values)
            {
                if (service is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch
                    {
                        // Log but don't throw during disposal
                    }
                }
            }

            _services.Clear();
            _factories.Clear();
            _disposed = true;
        }
    }


}