using System;
using System.Windows.Forms;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Extensions
{
    /// <summary>
    /// Extension methods for Windows Forms controls
    /// </summary>
    public static class ControlExtensions
    {
        /// <summary>
        /// Invokes an action on the UI thread if required
        /// </summary>
        public static void InvokeIfRequired(this Control control, Action action)
        {
            if (control.InvokeRequired)
                control.Invoke(action);
            else
                action();
        }

        /// <summary>
        /// Safely invokes an action on the UI thread
        /// </summary>
        public static void SafeInvoke(this Control control, Action action)
        {
            if (control?.InvokeRequired == true)
            {
                try
                {
                    control.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                    // Control was disposed, ignore
                }
                catch (InvalidOperationException)
                {
                    // Control handle not created or being disposed, ignore
                }
            }
            else if (!control?.IsDisposed == true)
            {
                action();
            }
        }

        /// <summary>
        /// Safely begins an invoke on the UI thread
        /// </summary>
        public static void SafeBeginInvoke(this Control control, Action action)
        {
            if (control?.InvokeRequired == true)
            {
                try
                {
                    control.BeginInvoke(action);
                }
                catch (ObjectDisposedException)
                {
                    // Control was disposed, ignore
                }
                catch (InvalidOperationException)
                {
                    // Control handle not created or being disposed, ignore
                }
            }
            else if (!control?.IsDisposed == true)
            {
                action();
            }
        }

        /// <summary>
        /// Updates a control's property safely on the UI thread
        /// </summary>
        public static void SafeUpdateProperty<T>(this Control control, Action<T> setter, T value)
            where T : class
        {
            control.SafeInvoke(() => setter(value));
        }
    }
}