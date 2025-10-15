using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.Core;
using ShalevOhad.DCS.SRS.Recorder.Core.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Controls
{
    /// <summary>
    /// A custom seek bar that displays audio waveform data and allows seeking
    /// </summary>
    public class WaveformSeekBar : Control
    {
        #region Private Fields

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private float[] _waveformData = Array.Empty<float>();
        private int _position = 0;
        private int _maximum = 1000;
        private Color _waveformColor = Color.FromArgb(100, 150, 255);
        private Color _progressColor = Color.FromArgb(0, 120, 215);
        private Color _backgroundColor = Color.FromArgb(240, 240, 240);
        private Color _gridColor = Color.FromArgb(220, 220, 220);
        private Color _positionLineColor = Color.Red;
        private Color _zoomIndicatorColor = Color.FromArgb(100, 255, 165, 0);
        private Color _timeLabelColor = Color.FromArgb(60, 60, 60);
        private Color _currentTimeLabelColor = Color.FromArgb(180, 0, 0);
        private bool _isDragging = false;
        private bool _showGrid = true;
        private bool _showZoomIndicator = true;
        private bool _showTimeLabels = true;
        private bool _showCurrentTimeLabel = true;
        private int _gridInterval = 100; // Grid lines every 100 units
        
        // Frequency filtering fields
        private HashSet<(double Frequency, Modulation Modulation)> _selectedFrequencyModulations = new();
        private bool _isFrequencyFilterEnabled = false;
        
        // Zoom functionality fields
        private float _zoomLevel = 1.0f; // 1.0 = no zoom, 2.0 = 2x zoom, 0.5 = zoomed out
        private int _zoomCenter = 0; // Position in the original data that is centered in the view
        private const float MIN_ZOOM = 0.1f; // Maximum zoom out (10% of original)
        private const float MAX_ZOOM = 10.0f; // Maximum zoom in (1000% of original)
        private const float ZOOM_STEP = 1.2f; // Zoom multiplier per scroll step
        
        // Timeline mapping fields
        private TimeSpan _totalDuration = TimeSpan.Zero;
        private DateTime _recordingStart = DateTime.MinValue;
        private List<AudioPacketMetadata> _filteredPackets = new();
        
        // Context menu
        private ContextMenuStrip? _contextMenu;
        
        // Opus decoder for waveform generation (shared instance)
        private OpusDecoder? _waveformOpusDecoder;
        
        #endregion

        #region Events

        /// <summary>Occurs when the position changes due to user interaction</summary>
        public event EventHandler<int>? PositionChanged;
        
        /// <summary>Occurs when the user starts dragging</summary>
        public event EventHandler? SeekStarted;
        
        /// <summary>Occurs when the user stops dragging</summary>
        public event EventHandler? SeekCompleted;
        
        /// <summary>Occurs when the zoom level changes</summary>
        public event EventHandler<float>? ZoomChanged;

        #endregion

        #region Public Properties

        /// <summary>Gets or sets the current position (0 to Maximum)</summary>
        [Category("Behavior")]
        [Description("The current position of the seek bar")]
        [DefaultValue(0)]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int Position
        {
            get => _position;
            set
            {
                var newValue = Math.Max(0, Math.Min(_maximum, value));
                if (_position != newValue)
                {
                    _position = newValue;
                    
                    // Always invalidate the control to ensure the position line updates
                    Invalidate();
                    
                    if (!_isDragging) // Only fire event if not dragging
                    {
                        PositionChanged?.Invoke(this, _position);
                    }
                }
            }
        }

        /// <summary>Gets or sets the maximum value</summary>
        [Category("Behavior")]
        [Description("The maximum value of the seek bar")]
        [DefaultValue(1000)]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int Maximum
        {
            get => _maximum;
            set
            {
                if (_maximum != value && value > 0)
                {
                    _maximum = value;
                    _position = Math.Min(_position, _maximum);
                    ValidateZoomCenter();
                    Invalidate();
                }
            }
        }

        /// <summary>Gets or sets the waveform color</summary>
        [Category("Appearance")]
        [Description("The color of the waveform")]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color WaveformColor
        {
            get => _waveformColor;
            set
            {
                if (_waveformColor != value)
                {
                    _waveformColor = value;
                    Invalidate();
                }
            }
        }

        /// <summary>Gets or sets the progress color</summary>
        [Category("Appearance")]
        [Description("The color of the progress area")]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color ProgressColor
        {
            get => _progressColor;
            set
            {
                if (_progressColor != value)
                {
                    _progressColor = value;
                    Invalidate();
                }
            }
        }

        /// <summary>Gets or sets whether to show grid lines</summary>
        [Category("Appearance")]
        [Description("Whether to show grid lines")]
        [DefaultValue(true)]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool ShowGrid
        {
            get => _showGrid;
            set
            {
                if (_showGrid != value)
                {
                    _showGrid = value;
                    Invalidate();
                }
            }
        }

        /// <summary>Gets or sets whether to show zoom indicator</summary>
        [Category("Appearance")]
        [Description("Whether to show zoom level indicator")]
        [DefaultValue(true)]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool ShowZoomIndicator
        {
            get => _showZoomIndicator;
            set
            {
                if (_showZoomIndicator != value)
                {
                    _showZoomIndicator = value;
                    Invalidate();
                }
            }
        }

        /// <summary>Gets or sets whether to show time labels at the start and end of visible range</summary>
        [Category("Appearance")]
        [Description("Whether to show time labels at the start and end of visible range")]
        [DefaultValue(true)]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool ShowTimeLabels
        {
            get => _showTimeLabels;
            set
            {
                if (_showTimeLabels != value)
                {
                    _showTimeLabels = value;
                    Invalidate();
                }
            }
        }

        /// <summary>Gets or sets whether to show current playing time label next to the position line</summary>
        [Category("Appearance")]
        [Description("Whether to show current playing time label next to the position line")]
        [DefaultValue(true)]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool ShowCurrentTimeLabel
        {
            get => _showCurrentTimeLabel;
            set
            {
                if (_showCurrentTimeLabel != value)
                {
                    _showCurrentTimeLabel = value;
                    Invalidate();
                }
            }
        }

        /// <summary>Gets or sets the color of time labels</summary>
        [Category("Appearance")]
        [Description("The color of time labels")]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color TimeLabelColor
        {
            get => _timeLabelColor;
            set
            {
                if (_timeLabelColor != value)
                {
                    _timeLabelColor = value;
                    Invalidate();
                }
            }
        }

        /// <summary>Gets or sets the color of the current time label</summary>
        [Category("Appearance")]
        [Description("The color of the current time label")]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color CurrentTimeLabelColor
        {
            get => _currentTimeLabelColor;
            set
            {
                if (_currentTimeLabelColor != value)
                {
                    _currentTimeLabelColor = value;
                    Invalidate();
                }
            }
        }

        /// <summary>Gets whether the user is currently dragging</summary>
        [Browsable(false)]
        public bool IsDragging => _isDragging;

        /// <summary>Gets or sets the current zoom level</summary>
        [Category("Behavior")]
        [Description("The current zoom level (1.0 = normal, >1.0 = zoomed in, <1.0 = zoomed out)")]
        [DefaultValue(1.0f)]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public float ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                var newZoom = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, value));
                if (Math.Abs(_zoomLevel - newZoom) > 0.001f)
                {
                    _zoomLevel = newZoom;
                    ValidateZoomCenter();
                    Invalidate();
                    ZoomChanged?.Invoke(this, _zoomLevel);
                }
            }
        }

        /// <summary>Gets or sets the center position for zooming</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ZoomCenter
        {
            get => _zoomCenter;
            set
            {
                _zoomCenter = Math.Max(0, Math.Min(_maximum, value));
                Invalidate();
            }
        }

        #endregion

        #region Constructor

        public WaveformSeekBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.UserPaint | 
                     ControlStyles.DoubleBuffer | 
                     ControlStyles.ResizeRedraw | 
                     ControlStyles.Selectable, true);
            
            Size = new Size(400, 60);
            BackColor = _backgroundColor;
            Cursor = Cursors.Hand;
            TabStop = true;
            
            InitializeContextMenu();
        }

        #endregion

        #region Public Methods

        /// <summary>Sets the waveform data from audio file analysis</summary>
        public async Task SetWaveformDataAsync(string filePath)
        {
            try
            {
                Logger.Info($"Generating waveform data for: {filePath}");
                
                var waveformResult = await Task.Run(() => GenerateWaveformData(filePath));
                
                _waveformData = waveformResult.WaveformData;
                _totalDuration = waveformResult.TotalDuration;
                _recordingStart = waveformResult.RecordingStart;
                _filteredPackets = waveformResult.FilteredPackets;
                
                Logger.Info($"Waveform data generated: {_waveformData.Length} points, duration: {_totalDuration}, packets: {_filteredPackets.Count}");
                
                Invalidate();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to generate waveform data");
                _waveformData = Array.Empty<float>();
                _totalDuration = TimeSpan.Zero;
                _recordingStart = DateTime.MinValue;
                _filteredPackets.Clear();
                Invalidate();
            }
        }

        /// <summary>Clears the waveform data</summary>
        public void ClearWaveform()
        {
            _waveformData = Array.Empty<float>();
            _totalDuration = TimeSpan.Zero;
            _recordingStart = DateTime.MinValue;
            _filteredPackets.Clear();
            Invalidate();
        }

        /// <summary>Sets the frequency filter for waveform generation</summary>
        public void SetFrequencyFilter(IEnumerable<(double Frequency, Modulation Modulation)> selectedFrequencyModulations, bool enabled)
        {
            _selectedFrequencyModulations = new HashSet<(double, Modulation)>(selectedFrequencyModulations);
            _isFrequencyFilterEnabled = enabled;
            
            Logger.Info($"Frequency filter updated: {_selectedFrequencyModulations.Count} combinations, enabled: {enabled}");
        }

        /// <summary>Clears the frequency filter</summary>
        public void ClearFrequencyFilter()
        {
            _selectedFrequencyModulations.Clear();
            _isFrequencyFilterEnabled = false;
            
            Logger.Info("Frequency filter cleared");
        }

        /// <summary>Regenerates waveform data with current frequency filter settings</summary>
        public async Task RefreshWaveformAsync(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                await SetWaveformDataAsync(filePath);
            }
        }

        /// <summary>Zooms in at the specified position</summary>
        public void ZoomIn(int centerPosition)
        {
            var newZoom = _zoomLevel * ZOOM_STEP;
            if (newZoom <= MAX_ZOOM)
            {
                _zoomCenter = centerPosition;
                ZoomLevel = newZoom;
                Logger.Debug($"Zoomed in to {_zoomLevel:F2}x at position {centerPosition}");
            }
        }

        /// <summary>Zooms out at the specified position</summary>
        public void ZoomOut(int centerPosition)
        {
            var newZoom = _zoomLevel / ZOOM_STEP;
            if (newZoom >= MIN_ZOOM)
            {
                _zoomCenter = centerPosition;
                ZoomLevel = newZoom;
                Logger.Debug($"Zoomed out to {_zoomLevel:F2}x at position {centerPosition}");
            }
        }

        /// <summary>Resets zoom to normal level (1.0x)</summary>
        public void ResetZoom()
        {
            _zoomLevel = 1.0f;
            _zoomCenter = _position;
            Invalidate();
            ZoomChanged?.Invoke(this, _zoomLevel);
            Logger.Debug("Zoom reset to 1.0x");
        }

        /// <summary>Zooms to fit the entire waveform in the view</summary>
        public void ZoomToFit()
        {
            _zoomLevel = 1.0f;
            _zoomCenter = _maximum / 2;
            Invalidate();
            ZoomChanged?.Invoke(this, _zoomLevel);
            Logger.Debug("Zoomed to fit entire waveform");
        }

        /// <summary>Zooms to selection (between two positions)</summary>
        public void ZoomToSelection(int startPosition, int endPosition)
        {
            if (startPosition >= endPosition) return;
            
            var selectionLength = endPosition - startPosition;
            var newZoomLevel = Math.Min(MAX_ZOOM, (float)_maximum / selectionLength);
            var centerPosition = (startPosition + endPosition) / 2;
            
            _zoomCenter = centerPosition;
            ZoomLevel = newZoomLevel;
            Logger.Debug($"Zoomed to selection {startPosition}-{endPosition} at {_zoomLevel:F2}x");
        }

        #endregion

        #region Protected Override Methods

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            
            var rect = ClientRectangle;
            
            // Fill background
            using (var bgBrush = new SolidBrush(_backgroundColor))
            {
                g.FillRectangle(bgBrush, rect);
            }
            
            // Draw zoom indicator if enabled and zoomed
            if (_showZoomIndicator && Math.Abs(_zoomLevel - 1.0f) > 0.001f)
            {
                DrawZoomIndicator(g, rect);
            }
            
            // Draw grid if enabled
            if (_showGrid)
            {
                DrawGrid(g, rect);
            }
            
            // Draw waveform
            if (_waveformData.Length > 0)
            {
                DrawWaveform(g, rect);
            }
            
            // Draw progress overlay
            DrawProgress(g, rect);
            
            // Draw position line
            DrawPositionLine(g, rect);
            
            // Draw time labels if enabled
            if (_showTimeLabels && _totalDuration.TotalSeconds > 0)
            {
                DrawTimeLabels(g, rect);
            }
            
            // Draw current time label if enabled
            if (_showCurrentTimeLabel && _totalDuration.TotalSeconds > 0)
            {
                DrawCurrentTimeLabel(g, rect);
            }
            
            // Draw border
            using (var borderPen = new Pen(Color.Gray))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
            
            // Draw focus rectangle if focused
            if (Focused)
            {
                using (var focusPen = new Pen(Color.Blue, 1) { DashStyle = DashStyle.Dot })
                {
                    g.DrawRectangle(focusPen, 1, 1, Width - 3, Height - 3);
                }
            }
        }

        private void InitializeContextMenu()
        {
            _contextMenu = new ContextMenuStrip();
            
            _contextMenu.Items.Add("Zoom In", null, (s, e) => ZoomIn(_position));
            _contextMenu.Items.Add("Zoom Out", null, (s, e) => ZoomOut(_position));
            _contextMenu.Items.Add("-");
            _contextMenu.Items.Add("Reset Zoom", null, (s, e) => ResetZoom());
            _contextMenu.Items.Add("Zoom to Fit", null, (s, e) => ZoomToFit());
            _contextMenu.Items.Add("-");
            
            var gridMenuItem = new ToolStripMenuItem("Show Grid") { Checked = _showGrid };
            gridMenuItem.Click += (s, e) =>
            {
                ShowGrid = !ShowGrid;
                gridMenuItem.Checked = ShowGrid;
            };
            _contextMenu.Items.Add(gridMenuItem);
            
            var zoomIndicatorMenuItem = new ToolStripMenuItem("Show Zoom Indicator") { Checked = _showZoomIndicator };
            zoomIndicatorMenuItem.Click += (s, e) =>
            {
                ShowZoomIndicator = !ShowZoomIndicator;
                zoomIndicatorMenuItem.Checked = ShowZoomIndicator;
            };
            _contextMenu.Items.Add(zoomIndicatorMenuItem);
            
            var timeLabelsMenuItem = new ToolStripMenuItem("Show Time Labels") { Checked = _showTimeLabels };
            timeLabelsMenuItem.Click += (s, e) =>
            {
                ShowTimeLabels = !ShowTimeLabels;
                timeLabelsMenuItem.Checked = ShowTimeLabels;
            };
            _contextMenu.Items.Add(timeLabelsMenuItem);
            
            var currentTimeLabelMenuItem = new ToolStripMenuItem("Show Current Time") { Checked = _showCurrentTimeLabel };
            currentTimeLabelMenuItem.Click += (s, e) =>
            {
                ShowCurrentTimeLabel = !ShowCurrentTimeLabel;
                currentTimeLabelMenuItem.Checked = ShowCurrentTimeLabel;
            };
            _contextMenu.Items.Add(currentTimeLabelMenuItem);
        }

        private void DrawZoomIndicator(Graphics g, Rectangle rect)
        {
            var visibleRange = GetVisibleRange();
            var visibleStart = visibleRange.Start;
            var visibleEnd = visibleRange.End;
            var visibleWidth = visibleEnd - visibleStart;
            
            if (visibleWidth >= _maximum) return; // No zoom indicator needed when fully zoomed out
            
            // Draw a small indicator at the top showing the zoomed region
            var indicatorHeight = 4;
            var indicatorY = rect.Top + 2;
            var drawRect = new Rectangle(rect.X + 1, indicatorY, rect.Width - 2, indicatorHeight);
            
            // Background of the indicator (represents full range)
            using (var bgBrush = new SolidBrush(Color.FromArgb(50, Color.Gray)))
            {
                g.FillRectangle(bgBrush, drawRect);
            }
            
            // Visible range indicator
            var startX = drawRect.X + (int)((float)visibleStart / _maximum * drawRect.Width);
            var endX = drawRect.X + (int)((float)visibleEnd / _maximum * drawRect.Width);
            var visibleRect = new Rectangle(startX, indicatorY, Math.Max(1, endX - startX), indicatorHeight);
            
            using (var visibleBrush = new SolidBrush(_zoomIndicatorColor))
            {
                g.FillRectangle(visibleBrush, visibleRect);
            }
            
            // Draw zoom level text
            var zoomText = $"{_zoomLevel:F1}x";
            using (var font = new Font("Segoe UI", 7, FontStyle.Regular))
            using (var textBrush = new SolidBrush(Color.DarkGray))
            {
                var textSize = g.MeasureString(zoomText, font);
                var textX = rect.Right - textSize.Width - 4;
                var textY = indicatorY + indicatorHeight + 2;
                g.DrawString(zoomText, font, textBrush, textX, textY);
            }
        }

        private void DrawGrid(Graphics g, Rectangle rect)
        {
            if (_maximum <= 0) return;
            
            using (var gridPen = new Pen(_gridColor))
            {
                var drawRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
                
                // Calculate visible range based on zoom
                var visibleRange = GetVisibleRange();
                var visibleStart = visibleRange.Start;
                var visibleEnd = visibleRange.End;
                var visibleWidth = visibleEnd - visibleStart;
                
                if (visibleWidth <= 0) return;
                
                var pixelsPerUnit = (float)drawRect.Width / visibleWidth;
                
                // Calculate appropriate grid interval based on zoom level and time scale
                var timeBasedInterval = CalculateTimeBasedGridInterval();
                
                // Draw vertical grid lines for visible range
                var startGrid = (int)(Math.Ceiling((double)visibleStart / timeBasedInterval) * timeBasedInterval);
                for (int i = startGrid; i <= visibleEnd; i += timeBasedInterval)
                {
                    var relativePos = i - visibleStart;
                    var x = drawRect.X + (int)(relativePos * pixelsPerUnit);
                    
                    if (x >= drawRect.Left && x <= drawRect.Right)
                    {
                        g.DrawLine(gridPen, x, drawRect.Top, x, drawRect.Bottom);
                    }
                }
                
                // Draw horizontal center line
                var centerY = drawRect.Y + drawRect.Height / 2;
                g.DrawLine(gridPen, drawRect.Left, centerY, drawRect.Right, centerY);
            }
        }

        private void DrawTimeLabels(Graphics g, Rectangle rect)
        {
            if (_totalDuration.TotalSeconds <= 0) return;
            
            var visibleRange = GetVisibleRange();
            var visibleStart = visibleRange.Start;
            var visibleEnd = visibleRange.End;
            
            // Calculate start and end times for visible range
            var startTime = TimeSpan.FromTicks((long)((double)visibleStart / _maximum * _totalDuration.Ticks));
            var endTime = TimeSpan.FromTicks((long)((double)visibleEnd / _maximum * _totalDuration.Ticks));
            
            using (var font = new Font("Segoe UI", 8, FontStyle.Regular))
            using (var textBrush = new SolidBrush(_timeLabelColor))
            using (var backgroundBrush = new SolidBrush(Color.FromArgb(200, _backgroundColor)))
            {
                var drawRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
                
                // Format time strings
                var startTimeText = FormatTimeForLabel(startTime);
                var endTimeText = FormatTimeForLabel(endTime);
                
                // Draw start time label (top-left)
                var startTimeSize = g.MeasureString(startTimeText, font);
                var startTimeBounds = new Rectangle(
                    drawRect.Left + 4,
                    drawRect.Top + 4,
                    (int)startTimeSize.Width + 4,
                    (int)startTimeSize.Height + 2
                );
                g.FillRectangle(backgroundBrush, startTimeBounds);
                g.DrawString(startTimeText, font, textBrush, startTimeBounds.Left + 2, startTimeBounds.Top + 1);
                
                // Draw end time label (top-right)
                var endTimeSize = g.MeasureString(endTimeText, font);
                var endTimeBounds = new Rectangle(
                    drawRect.Right - (int)endTimeSize.Width - 8,
                    drawRect.Top + 4,
                    (int)endTimeSize.Width + 4,
                    (int)endTimeSize.Height + 2
                );
                g.FillRectangle(backgroundBrush, endTimeBounds);
                g.DrawString(endTimeText, font, textBrush, endTimeBounds.Left + 2, endTimeBounds.Top + 1);
            }
        }

        private void DrawCurrentTimeLabel(Graphics g, Rectangle rect)
        {
            if (_totalDuration.TotalSeconds <= 0) return;
            
            var drawRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
            
            // Get visible range based on zoom
            var visibleRange = GetVisibleRange();
            var visibleStart = visibleRange.Start;
            var visibleEnd = visibleRange.End;
            var visibleWidth = visibleEnd - visibleStart;
            
            if (visibleWidth <= 0) return;
            
            // Only draw current time label if position is within the visible range
            if (_position >= visibleStart && _position <= visibleEnd)
            {
                var relativePosition = _position - visibleStart;
                var x = drawRect.X + (int)((float)relativePosition / visibleWidth * drawRect.Width);
                
                // Calculate current time
                var currentTime = TimeSpan.FromTicks((long)((double)_position / _maximum * _totalDuration.Ticks));
                var currentTimeText = FormatTimeForLabel(currentTime);
                
                using (var font = new Font("Segoe UI", 8, FontStyle.Bold))
                using (var textBrush = new SolidBrush(_currentTimeLabelColor))
                using (var backgroundBrush = new SolidBrush(Color.FromArgb(220, Color.White)))
                {
                    var textSize = g.MeasureString(currentTimeText, font);
                    
                    // Position the label to the right of the position line, but keep it within bounds
                    var labelX = x + 6;
                    if (labelX + textSize.Width > drawRect.Right - 4)
                    {
                        labelX = x - (int)textSize.Width - 6; // Move to left of position line
                    }
                    
                    // Position vertically in the middle area
                    var labelY = drawRect.Top + (drawRect.Height / 2) - (int)(textSize.Height / 2);
                    
                    var labelBounds = new Rectangle(
                        labelX,
                        labelY,
                        (int)textSize.Width + 4,
                        (int)textSize.Height + 2
                    );
                    
                    // Draw background with slight border
                    using (var borderPen = new Pen(_currentTimeLabelColor, 1))
                    {
                        g.FillRectangle(backgroundBrush, labelBounds);
                        g.DrawRectangle(borderPen, labelBounds);
                    }
                    
                    g.DrawString(currentTimeText, font, textBrush, labelBounds.Left + 2, labelBounds.Top + 1);
                }
            }
        }

        private string FormatTimeForLabel(TimeSpan time)
        {
            if (time.TotalHours >= 1)
            {
                return time.ToString(@"h\:mm\:ss");
            }
            else if (time.TotalMinutes >= 1)
            {
                return time.ToString(@"m\:ss");
            }
            else
            {
                return time.ToString(@"s\.ff") + "s";
            }
        }

        private int CalculateTimeBasedGridInterval()
        {
            if (_totalDuration == TimeSpan.Zero) return _gridInterval;
            
            // Calculate interval based on visible time range
            var visibleRange = GetVisibleRange();
            var visibleWidth = visibleRange.End - visibleRange.Start;
            var visibleTimeSpan = TimeSpan.FromTicks((long)((double)visibleWidth / _maximum * _totalDuration.Ticks));
            
            // Select appropriate time intervals
            if (visibleTimeSpan.TotalHours >= 1)
                return (int)(_maximum * (10.0 / 60.0) / _totalDuration.TotalMinutes); // 10 minute intervals
            else if (visibleTimeSpan.TotalMinutes >= 10)
                return (int)(_maximum * (1.0 / 60.0) / _totalDuration.TotalMinutes); // 1 minute intervals
            else if (visibleTimeSpan.TotalMinutes >= 1)
                return (int)(_maximum * (10.0 / 3600.0) / _totalDuration.TotalMinutes); // 10 second intervals
            else
                return (int)(_maximum * (1.0 / 3600.0) / _totalDuration.TotalMinutes); // 1 second intervals
        }

        private void DrawWaveform(Graphics g, Rectangle rect)
        {
            if (_waveformData.Length == 0) return;
            
            var drawRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
            var centerY = drawRect.Y + drawRect.Height / 2;
            var maxAmplitude = drawRect.Height / 2 - 2;
            
            // Get visible range based on zoom
            var visibleRange = GetVisibleRange();
            var visibleStart = visibleRange.Start;
            var visibleEnd = visibleRange.End;
            var visibleWidth = visibleEnd - visibleStart;
            
            if (visibleWidth <= 0) return;
            
            using (var waveformBrush = new SolidBrush(_waveformColor))
            {
                // Map seekbar position to waveform data using timeline
                var visibleStartRatio = (double)visibleStart / _maximum;
                var visibleEndRatio = (double)visibleEnd / _maximum;
                
                var waveformStartIndex = (int)(visibleStartRatio * _waveformData.Length);
                var waveformEndIndex = Math.Min(_waveformData.Length, (int)(visibleEndRatio * _waveformData.Length));
                var visibleWaveformWidth = waveformEndIndex - waveformStartIndex;
                
                if (visibleWaveformWidth <= 0) return;
                
                // Calculate how many waveform samples per pixel for the visible range
                var samplesPerPixel = Math.Max(1, visibleWaveformWidth / drawRect.Width);
                
                for (int x = 0; x < drawRect.Width; x++)
                {
                    var startSample = waveformStartIndex + (x * samplesPerPixel);
                    var endSample = Math.Min(waveformStartIndex + ((x + 1) * samplesPerPixel), waveformEndIndex);
                    
                    if (startSample >= _waveformData.Length) break;
                    
                    // Find peak amplitude in this pixel's range
                    var peak = 0f;
                    for (int i = startSample; i < endSample && i < _waveformData.Length; i++)
                    {
                        peak = Math.Max(peak, Math.Abs(_waveformData[i]));
                    }
                    
                    // Draw waveform bar
                    var amplitude = (int)(peak * maxAmplitude);
                    if (amplitude > 0)
                    {
                        var barRect = new Rectangle(
                            drawRect.X + x,
                            centerY - amplitude,
                            1,
                            amplitude * 2
                        );
                        
                        g.FillRectangle(waveformBrush, barRect);
                    }
                }
            }
        }

        private void DrawProgress(Graphics g, Rectangle rect)
        {
            if (_maximum <= 0) return;
            
            var drawRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
            
            // Get visible range based on zoom
            var visibleRange = GetVisibleRange();
            var visibleStart = visibleRange.Start;
            var visibleEnd = visibleRange.End;
            var visibleWidth = visibleEnd - visibleStart;
            
            if (visibleWidth <= 0) return;
            
            // Calculate progress within visible range
            if (_position >= visibleStart && _position <= visibleEnd)
            {
                var relativeProgress = _position - visibleStart;
                var progressWidth = (int)((float)relativeProgress / visibleWidth * drawRect.Width);
                
                if (progressWidth > 0)
                {
                    var progressRect = new Rectangle(drawRect.X, drawRect.Y, progressWidth, drawRect.Height);
                    
                    using (var progressBrush = new SolidBrush(Color.FromArgb(80, _progressColor)))
                    {
                        g.FillRectangle(progressBrush, progressRect);
                    }
                }
            }
            else if (_position < visibleStart)
            {
                // Progress is completely before visible range - no progress bar visible
            }
            else if (_position > visibleEnd)
            {
                // Progress is completely after visible range - fill entire visible area
                using (var progressBrush = new SolidBrush(Color.FromArgb(80, _progressColor)))
                {
                    g.FillRectangle(progressBrush, drawRect);
                }
            }
        }

        private void DrawPositionLine(Graphics g, Rectangle rect)
        {
            if (_maximum <= 0) return;
            
            var drawRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
            
            // Get visible range based on zoom
            var visibleRange = GetVisibleRange();
            var visibleStart = visibleRange.Start;
            var visibleEnd = visibleRange.End;
            var visibleWidth = visibleEnd - visibleStart;
            
            if (visibleWidth <= 0) return;
            
            // Only draw position line if it's within the visible range
            if (_position >= visibleStart && _position <= visibleEnd)
            {
                var relativePosition = _position - visibleStart;
                var x = drawRect.X + (int)((float)relativePosition / visibleWidth * drawRect.Width);
                
                using (var positionPen = new Pen(_positionLineColor, 2))
                {
                    g.DrawLine(positionPen, x, drawRect.Top, x, drawRect.Bottom);
                }
                
                // Add trace logging for position line updates
                Logger.Trace($"Drew position line at x={x}, position={_position}, visible={visibleStart}-{visibleEnd}");
            }
            else
            {
                Logger.Trace($"Position line not visible: position={_position}, visible={visibleStart}-{visibleEnd}");
            }
        }

        private void UpdatePositionFromMouse(int mouseX)
        {
            var drawRect = new Rectangle(1, 1, Width - 2, Height - 2);
            var relativeX = Math.Max(0, Math.Min(mouseX - drawRect.X, drawRect.Width));
            
            // Get visible range based on zoom
            var visibleRange = GetVisibleRange();
            var visibleStart = visibleRange.Start;
            var visibleEnd = visibleRange.End;
            var visibleWidth = visibleEnd - visibleStart;
            
            if (visibleWidth <= 0) return;
            
            // Calculate position within visible range
            var relativePosition = (float)relativeX / drawRect.Width * visibleWidth;
            var newPosition = (int)(visibleStart + relativePosition);
            
            Position = newPosition;
        }

        private WaveformGenerationResult GenerateWaveformData(string filePath)
        {
            try
            {
                var samples = new List<float>();
                var filteredPackets = new List<AudioPacketMetadata>();
                var reader = new AudioPacketReader(filePath);
                DateTime? recordingStart = null;
                DateTime? recordingEnd = null;
                
                Logger.Info($"Generating waveform data with frequency filter: enabled={_isFrequencyFilterEnabled}, combinations={_selectedFrequencyModulations.Count}");
                
                // Initialize Opus decoder for waveform generation
                InitializeWaveformOpusDecoder();
                
                // Read all audio packets and extract amplitude data
                foreach (var packet in reader.ReadAllPackets())
                {
                    recordingStart ??= packet.Timestamp;
                    recordingEnd = packet.Timestamp;
                    
                    if (packet.AudioPayload.Length > 0)
                    {
                        // Apply frequency filter if enabled
                        if (_isFrequencyFilterEnabled)
                        {
                            var packetModulation = (Modulation)packet.Modulation; // Cast byte to Modulation enum
                            var packetKey = (packet.Frequency, packetModulation);
                            if (!_selectedFrequencyModulations.Contains(packetKey))
                            {
                                // Skip this packet as it's filtered out
                                continue;
                            }
                        }
                        
                        filteredPackets.Add(packet);
                        
                        // Convert audio payload to amplitude samples using proper Opus decoding
                        var packetSamples = ConvertAudioToAmplitude(packet);
                        samples.AddRange(packetSamples);
                    }
                }
                
                reader.Dispose();
                
                var totalDuration = recordingEnd.HasValue && recordingStart.HasValue 
                    ? recordingEnd.Value - recordingStart.Value 
                    : TimeSpan.Zero;
                
                if (samples.Count == 0)
                {
                    Logger.Warn("No audio samples found after filtering - returning placeholder waveform");
                    return new WaveformGenerationResult
                    {
                        WaveformData = GeneratePlaceholderWaveform(),
                        TotalDuration = totalDuration,
                        RecordingStart = recordingStart ?? DateTime.MinValue,
                        FilteredPackets = filteredPackets
                    };
                }
                
                Logger.Info($"Generated waveform from {samples.Count} filtered audio samples, duration: {totalDuration}");
                
                // Downsample for visualization (aim for ~1000-2000 points)
                var targetSamples = Math.Min(2000, Math.Max(100, samples.Count / 100));
                var downsampledData = DownsampleAudio(samples.ToArray(), targetSamples);
                
                return new WaveformGenerationResult
                {
                    WaveformData = downsampledData,
                    TotalDuration = totalDuration,
                    RecordingStart = recordingStart ?? DateTime.MinValue,
                    FilteredPackets = filteredPackets
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating waveform data");
                
                // Return a placeholder waveform
                return new WaveformGenerationResult
                {
                    WaveformData = GeneratePlaceholderWaveform(),
                    TotalDuration = TimeSpan.Zero,
                    RecordingStart = DateTime.MinValue,
                    FilteredPackets = new List<AudioPacketMetadata>()
                };
            }
            finally
            {
                // Clean up the opus decoder
                DisposeWaveformOpusDecoder();
            }
        }

        private void InitializeWaveformOpusDecoder()
        {
            try
            {
                DisposeWaveformOpusDecoder(); // Clean up any existing decoder
                _waveformOpusDecoder = OpusDecoder.Create(Constants.OUTPUT_SAMPLE_RATE, 1);
                _waveformOpusDecoder.ForwardErrorCorrection = true;
                Logger.Debug("Initialized Opus decoder for waveform generation");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize Opus decoder for waveform generation");
                _waveformOpusDecoder = null;
            }
        }

        private void DisposeWaveformOpusDecoder()
        {
            try
            {
                _waveformOpusDecoder?.Dispose();
                _waveformOpusDecoder = null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error disposing waveform Opus decoder");
            }
        }

        private float[] ConvertAudioToAmplitude(AudioPacketMetadata packet)
        {
            try
            {
                float[] audioSamples;
                
                // Check if the audio is Opus-encoded
                if (Helpers.IsOpusEncoded(packet))
                {
                    // Decode Opus audio to PCM samples
                    audioSamples = DecodeOpusToFloat(packet.AudioPayload);
                }
                else
                {
                    // Assume it's already PCM and convert to float
                    audioSamples = Helpers.ConvertPcm16ToFloat(packet.AudioPayload);
                }
                
                if (audioSamples == null || audioSamples.Length == 0)
                {
                    Logger.Trace($"No audio samples decoded from packet, returning empty array");
                    return Array.Empty<float>();
                }
                
                // Convert to amplitude values (absolute values)
                var amplitudes = new float[audioSamples.Length];
                for (int i = 0; i < audioSamples.Length; i++)
                {
                    amplitudes[i] = Math.Abs(audioSamples[i]);
                }
                
                return amplitudes;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error converting audio to amplitude");
                return Array.Empty<float>();
            }
        }

        private float[] DecodeOpusToFloat(byte[] opusData)
        {
            if (_waveformOpusDecoder == null)
            {
                Logger.Warn("Opus decoder not initialized for waveform generation");
                return Array.Empty<float>();
            }
            
            try
            {
                // Calculate expected sample count based on standard Opus frame duration
                const int expectedSamples = Constants.OUTPUT_SAMPLE_RATE * Constants.OPUS_FRAME_DURATION_MS / 1000;
                var buffer = new float[expectedSamples];
                
                int samplesDecoded = _waveformOpusDecoder.DecodeFloat(opusData, buffer.AsMemory());
                
                if (samplesDecoded > 0)
                {
                    if (samplesDecoded < buffer.Length)
                    {
                        Array.Resize(ref buffer, samplesDecoded);
                    }
                    return buffer;
                }
                else
                {
                    Logger.Trace("No samples decoded from Opus data");
                    return Array.Empty<float>();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to decode Opus audio for waveform generation");
                return Array.Empty<float>();
            }
        }

        private float[] DownsampleAudio(float[] input, int targetLength)
        {
            if (input.Length <= targetLength)
                return input;
            
            var output = new float[targetLength];
            var samplesPerOutput = (float)input.Length / targetLength;
            
            for (int i = 0; i < targetLength; i++)
            {
                var startIdx = (int)(i * samplesPerOutput);
                var endIdx = (int)((i + 1) * samplesPerOutput);
                endIdx = Math.Min(endIdx, input.Length);
                
                // Find peak amplitude in this range
                var peak = 0f;
                for (int j = startIdx; j < endIdx; j++)
                {
                    peak = Math.Max(peak, input[j]);
                }
                
                output[i] = peak;
            }
            
            return output;
        }

        private float[] GeneratePlaceholderWaveform()
        {
            // Generate a simple sine wave pattern as placeholder
            var samples = new float[200];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (float)(Math.Abs(Math.Sin(i * 0.1)) * 0.5);
            }
            return samples;
        }

        private void ValidateZoomCenter()
        {
            // Ensure the zoom center is within the valid range after zoom level change
            if (_zoomCenter < 0) _zoomCenter = 0;
            if (_zoomCenter > _maximum) _zoomCenter = _maximum;
        }

        private int GetDataPositionFromMouse(int mouseX)
        {
            var drawRect = new Rectangle(1, 1, Width - 2, Height - 2);
            var relativeX = Math.Max(0, Math.Min(mouseX - drawRect.X, drawRect.Width));
            
            // Get visible range based on zoom
            var visibleRange = GetVisibleRange();
            var visibleStart = visibleRange.Start;
            var visibleEnd = visibleRange.End;
            var visibleWidth = visibleEnd - visibleStart;
            
            if (visibleWidth <= 0) return _position;
            
            // Calculate position within visible range
            var relativePosition = (float)relativeX / drawRect.Width * visibleWidth;
            var dataPosition = (int)(visibleStart + relativePosition);
            
            return Math.Max(0, Math.Min(_maximum, dataPosition));
        }

        private (int Start, int End) GetVisibleRange()
        {
            var center = _zoomCenter;
            var range = (int)(_maximum / _zoomLevel);
            
            var start = Math.Max(0, center - range / 2);
            var end = Math.Min(_maximum, center + range / 2);
            
            // Ensure we always have a positive range
            if (end <= start)
            {
                end = start + 1;
            }
            
            return (start, end);
        }

        #endregion

        #region Mouse and Keyboard Events

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus(); // Ensure control has focus for keyboard events
            
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                Capture = true;
                
                UpdatePositionFromMouse(e.X);
                SeekStarted?.Invoke(this, EventArgs.Empty);
            }
            else if (e.Button == MouseButtons.Right)
            {
                // Show context menu
                _contextMenu?.Show(this, e.Location);
            }
            
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isDragging)
            {
                UpdatePositionFromMouse(e.X);
            }
            
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _isDragging)
            {
                _isDragging = false;
                Capture = false;
                
                UpdatePositionFromMouse(e.X);
                PositionChanged?.Invoke(this, _position);
                SeekCompleted?.Invoke(this, EventArgs.Empty);
            }
            
            base.OnMouseUp(e);
        }

        protected override void OnResize(EventArgs e)
        {
            Invalidate();
            base.OnResize(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (ModifierKeys.HasFlag(Keys.Control))
            {
                // Convert mouse X position to data position for zoom center
                var mousePosition = GetDataPositionFromMouse(e.X);
                
                if (e.Delta > 0)
                {
                    ZoomIn(mousePosition);
                }
                else
                {
                    ZoomOut(mousePosition);
                }
            }
            else
            {
                // Normal scroll behavior - pan the view
                if (_zoomLevel > 1.0f)
                {
                    var panAmount = (int)(_maximum * 0.1f / _zoomLevel); // Pan amount relative to zoom level
                    var newCenter = _zoomCenter + (e.Delta > 0 ? -panAmount : panAmount);
                    ZoomCenter = newCenter;
                }
            }
            
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (ModifierKeys.HasFlag(Keys.Control))
                {
                    // Ctrl+Double-click = Reset zoom
                    ResetZoom();
                }
                else
                {
                    // Double-click = Zoom in at mouse position
                    var mousePosition = GetDataPositionFromMouse(e.X);
                    ZoomIn(mousePosition);
                }
            }
            
            base.OnMouseDoubleClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var handled = true;
            
            switch (e.KeyCode)
            {
                case Keys.Add:
                case Keys.Oemplus:
                    if (e.Control)
                        ZoomIn(_position);
                    break;
                    
                case Keys.Subtract:
                case Keys.OemMinus:
                    if (e.Control)
                        ZoomOut(_position);
                    break;
                    
                case Keys.D0:
                case Keys.NumPad0:
                    if (e.Control)
                        ResetZoom();
                    break;
                    
                case Keys.Home:
                    if (e.Control)
                        ZoomToFit();
                    else
                        Position = 0;
                    break;
                    
                case Keys.End:
                    Position = _maximum;
                    break;
                    
                case Keys.Left:
                    var leftStep = e.Control ? _maximum / 100 : _maximum / 1000;
                    Position = Math.Max(0, _position - leftStep);
                    break;
                    
                case Keys.Right:
                    var rightStep = e.Control ? _maximum / 100 : _maximum / 1000;
                    Position = Math.Min(_maximum, _position + rightStep);
                    break;
                    
                default:
                    handled = false;
                    break;
            }
            
            if (handled)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            
            base.OnKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Home:
                case Keys.End:
                case Keys.Add:
                case Keys.Subtract:
                case Keys.Oemplus:
                case Keys.OemMinus:
                case Keys.D0:
                case Keys.NumPad0:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        #endregion
    }

    /// <summary>
    /// Result structure for waveform generation
    /// </summary>
    internal class WaveformGenerationResult
    {
        public float[] WaveformData { get; set; } = Array.Empty<float>();
        public TimeSpan TotalDuration { get; set; }
        public DateTime RecordingStart { get; set; }
        public List<AudioPacketMetadata> FilteredPackets { get; set; } = new();
    }
}