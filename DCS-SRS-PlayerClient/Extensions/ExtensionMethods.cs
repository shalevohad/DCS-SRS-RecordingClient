using System;
using System.Collections.Generic;
using System.Linq;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Extensions
{
    /// <summary>
    /// Extension methods for better integration between legacy and new code
    /// </summary>
    public static class ModelExtensions
    {
        /// <summary>
        /// Gets a display-friendly text for frequency modulation info
        /// </summary>
        public static string GetDisplayText(this FrequencyModulationInfo info)
        {
            return $"{info.Frequency:F3} MHz - {info.Modulation} ({info.Players.Count} player{(info.Players.Count != 1 ? "s" : "")})";
        }

        /// <summary>
        /// Gets a display-friendly text for player frequency info
        /// </summary>
        public static string GetDisplayText(this PlayerFrequencyInfo info)
        {
            var aircraftInfo = !string.IsNullOrEmpty(info.Aircraft) ? $" ({info.Aircraft})" : "";
            return $"{info.Name}{aircraftInfo}";
        }

        /// <summary>
        /// Gets the display name for a player with fallback to GUID
        /// </summary>
        public static string GetDisplayName(this SRClientBase? client)
        {
            if (client == null) return "Unknown";
            return !string.IsNullOrEmpty(client.Name) ? client.Name : client.ClientGuid;
        }

        /// <summary>
        /// Gets the coalition name for display
        /// </summary>
        public static string GetCoalitionName(this SRClientBase? client)
        {
            if (client == null) return "Unknown";
            
            return client.Coalition switch
            {
                0 => "Spectator",
                1 => "Red",
                2 => "Blue",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Safely converts a list to HashSet for performance
        /// </summary>
        public static HashSet<T> ToHashSetSafe<T>(this IEnumerable<T>? source)
        {
            return source?.ToHashSet() ?? new HashSet<T>();
        }

        /// <summary>
        /// Safely gets count with null check
        /// </summary>
        public static int CountSafe<T>(this IEnumerable<T>? source)
        {
            return source?.Count() ?? 0;
        }

        /// <summary>
        /// Safely checks if any items exist
        /// </summary>
        public static bool AnySafe<T>(this IEnumerable<T>? source)
        {
            return source?.Any() ?? false;
        }

        /// <summary>
        /// Safely checks if any items match predicate
        /// </summary>
        public static bool AnySafe<T>(this IEnumerable<T>? source, Func<T, bool> predicate)
        {
            return source?.Any(predicate) ?? false;
        }
    }

    /// <summary>
    /// Extension methods for collections and LINQ operations
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Partitions a collection into chunks of the specified size
        /// </summary>
        public static IEnumerable<List<T>> Chunk<T>(this IEnumerable<T> source, int size)
        {
            if (size <= 0) throw new ArgumentException("Chunk size must be greater than 0", nameof(size));

            var chunk = new List<T>(size);
            foreach (var item in source)
            {
                chunk.Add(item);
                if (chunk.Count == size)
                {
                    yield return chunk;
                    chunk = new List<T>(size);
                }
            }

            if (chunk.Count > 0)
            {
                yield return chunk;
            }
        }

        /// <summary>
        /// Performs an action on each element in the collection
        /// </summary>
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
            {
                action(item);
            }
        }

        /// <summary>
        /// Performs an action on each element with its index
        /// </summary>
        public static void ForEachWithIndex<T>(this IEnumerable<T> source, Action<T, int> action)
        {
            var index = 0;
            foreach (var item in source)
            {
                action(item, index++);
            }
        }

        /// <summary>
        /// Safely gets an item from a dictionary or returns default
        /// </summary>
        public static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue? defaultValue = default)
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }

    /// <summary>
    /// Extension methods for async operations
    /// </summary>
    public static class AsyncExtensions
    {
        /// <summary>
        /// Executes an async operation with a timeout
        /// </summary>
        public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
        {
            using var cts = new System.Threading.CancellationTokenSource(timeout);
            var timeoutTask = Task.Delay(timeout, cts.Token);
            var completedTask = await Task.WhenAny(task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Operation timed out after {timeout}");
            }

            cts.Cancel(); // Cancel the timeout task
            return await task;
        }

        /// <summary>
        /// Executes an async operation with a timeout (void version)
        /// </summary>
        public static async Task WithTimeout(this Task task, TimeSpan timeout)
        {
            using var cts = new System.Threading.CancellationTokenSource(timeout);
            var timeoutTask = Task.Delay(timeout, cts.Token);
            var completedTask = await Task.WhenAny(task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Operation timed out after {timeout}");
            }

            cts.Cancel(); // Cancel the timeout task
            await task;
        }

        /// <summary>
        /// Safely awaits a task and handles common exceptions
        /// </summary>
        public static async Task<(bool Success, Exception? Exception)> TrySafeAsync(this Task task)
        {
            try
            {
                await task;
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex);
            }
        }

        /// <summary>
        /// Safely awaits a task and handles common exceptions (with result)
        /// </summary>
        public static async Task<(bool Success, T? Result, Exception? Exception)> TrySafeAsync<T>(this Task<T> task)
        {
            try
            {
                var result = await task;
                return (true, result, null);
            }
            catch (Exception ex)
            {
                return (false, default, ex);
            }
        }
    }
}