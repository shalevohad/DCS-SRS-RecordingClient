using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Controls
{
    /// <summary>
    /// User control for master volume adjustment
    /// </summary>
    public class VolumeControl : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private TrackBar? _volumeTrackBar;
        private Label? _volumeLabel;

        /// <summary>
        /// Raised when the volume value changes
        /// </summary>
        public event EventHandler<float>? VolumeChanged;

        /// <summary>
        /// Gets or sets the current volume value (0.0 - 2.0)
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float Volume
        {
            get => _volumeTrackBar?.Value / 100.0f ?? 1.0f;
            set
            {
                if (_volumeTrackBar != null)
                {
                    var clampedValue = Math.Clamp((int)(value * 100), 0, 200);
                    if (_volumeTrackBar.Value != clampedValue)
                    {
                        _volumeTrackBar.Value = clampedValue;
                        UpdateVolumeLabel();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current volume value (alias for Volume property for compatibility)
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float CurrentVolume => Volume;

        /// <summary>
        /// Gets or sets whether the volume control is enabled
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                if (_volumeTrackBar != null) _volumeTrackBar.Enabled = value;
                if (_volumeLabel != null) _volumeLabel.Enabled = value;
            }
        }

        public VolumeControl()
        {
            InitializeComponents();
            SetDefaultValues();
        }

        private void InitializeComponents()
        {
            SuspendLayout();
            
            // Set modern background
            BackColor = Color.FromArgb(45, 48, 55);

            // Volume trackbar with modern styling
            _volumeTrackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 200, // 0% to 200%
                Value = 100,   // Default 100%
                TickStyle = TickStyle.None,
                Location = new Point(0, 24),
                Size = new Size(140, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(45, 48, 55)
            };

            // Volume label with modern styling
            _volumeLabel = new Label
            {
                Text = "Volume: 100%",
                Location = new Point(0, 0),
                Size = new Size(140, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.FromArgb(45, 48, 55),
                TextAlign = ContentAlignment.TopCenter
            };

            // Custom paint for modern trackbar appearance
            _volumeTrackBar.Paint += OnTrackBarPaint;

            // Wire up events
            _volumeTrackBar.ValueChanged += OnVolumeTrackBarValueChanged;

            // Add controls
            Controls.Add(_volumeLabel);
            Controls.Add(_volumeTrackBar);

            // Set control size
            Size = new Size(140, 48);
            MinimumSize = new Size(120, 48);

            ResumeLayout(false);
        }

        private void SetDefaultValues()
        {
            Volume = 1.0f; // 100%
            UpdateVolumeLabel();
        }

        private void OnVolumeTrackBarValueChanged(object? sender, EventArgs e)
        {
            UpdateVolumeLabel();
            
            var volume = Volume;
            Logger.Debug($"Volume changed to: {volume:F2}");
            
            VolumeChanged?.Invoke(this, volume);
        }

        private void OnTrackBarPaint(object? sender, PaintEventArgs e)
        {
            if (_volumeTrackBar == null) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Calculate track rectangle
            var trackRect = new Rectangle(8, 10, _volumeTrackBar.Width - 16, 4);
            var thumbSize = 16;
            var thumbPos = (int)((float)(_volumeTrackBar.Value - _volumeTrackBar.Minimum) / 
                                (_volumeTrackBar.Maximum - _volumeTrackBar.Minimum) * 
                                (trackRect.Width - thumbSize)) + trackRect.X;

            // Draw track background
            using (var trackBrush = new SolidBrush(Color.FromArgb(60, 60, 70)))
            {
                e.Graphics.FillRoundedRectangle(trackBrush, trackRect, 2);
            }

            // Draw filled portion of track
            var filledRect = new Rectangle(trackRect.X, trackRect.Y, thumbPos - trackRect.X + thumbSize / 2, trackRect.Height);
            using (var filledBrush = new SolidBrush(Color.FromArgb(100, 160, 255)))
            {
                e.Graphics.FillRoundedRectangle(filledBrush, filledRect, 2);
            }

            // Draw thumb
            var thumbRect = new Rectangle(thumbPos, trackRect.Y - 6, thumbSize, thumbSize);
            using (var thumbBrush = new SolidBrush(Color.FromArgb(120, 180, 255)))
            using (var thumbPen = new Pen(Color.FromArgb(80, 140, 215), 2))
            {
                e.Graphics.FillEllipse(thumbBrush, thumbRect);
                e.Graphics.DrawEllipse(thumbPen, thumbRect);
            }
        }

        private void UpdateVolumeLabel()
        {
            if (_volumeLabel != null && _volumeTrackBar != null)
            {
                _volumeLabel.Text = $"Volume: {_volumeTrackBar.Value}%";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _volumeTrackBar?.Dispose();
                _volumeLabel?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Extension methods for drawing rounded rectangles
    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle rect, int radius)
        {
            using (var path = CreateRoundedRectanglePath(rect, radius))
            {
                graphics.FillPath(brush, path);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;

            if (diameter > rect.Width || diameter > rect.Height)
            {
                path.AddRectangle(rect);
                return path;
            }

            // Top left arc
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            // Top right arc
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            // Bottom right arc
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            // Bottom left arc
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}