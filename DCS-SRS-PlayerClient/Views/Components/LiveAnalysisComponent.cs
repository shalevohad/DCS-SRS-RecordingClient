using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Components
{
    /// <summary>
    /// Enhanced component for real-time audio analysis display
    /// </summary>
    public class LiveAnalysisComponent : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Fields

        private Panel _mainPanel;
        private Panel _controlsPanel;
        private Panel _chartsPanel;
        private Panel _frequencyPanel;
        private Panel _playerPanel;
        private Panel _modulationPanel;
        private Panel _statisticsPanel;

        private CheckBox _enableAnalysisCheckBox;
        private ComboBox _analysisWindowComboBox;
        private Label _statusLabel;

        private LiveAnalysisStats _currentStats = LiveAnalysisStats.Empty;
        private AnalysisConfig _config = AnalysisConfig.Default;
        private System.Windows.Forms.Timer _updateTimer;

        // Chart colors
        private readonly Color[] _chartColors =
        {
            Color.FromArgb(100, 150, 255),
            Color.FromArgb(255, 100, 150),
            Color.FromArgb(150, 255, 100),
            Color.FromArgb(255, 200, 100),
            Color.FromArgb(200, 100, 255),
            Color.FromArgb(100, 255, 200)
        };

        #endregion

        #region Events

        public event EventHandler<AnalysisConfig>? ConfigChanged;
        public event EventHandler<string>? StatusChanged;

        #endregion

        #region Properties

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public LiveAnalysisStats CurrentStats
        {
            get => _currentStats;
            set
            {
                _currentStats = value;
                InvalidateCharts();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AnalysisConfig Config
        {
            get => _config;
            set
            {
                _config = value;
                UpdateConfigControls();
                ConfigChanged?.Invoke(this, _config);
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsAnalysisEnabled
        {
            get => _config.EnableRealTimeAnalysis;
            set
            {
                if (_config.EnableRealTimeAnalysis != value)
                {
                    Config = _config with { EnableRealTimeAnalysis = value };
                }
            }
        }

        #endregion

        #region Constructor

        public LiveAnalysisComponent()
        {
            InitializeComponent();
            CreateControls();
            SetupTimer();
            UpdateConfigControls();
        }

        #endregion

        #region Initialization

        private void CreateControls()
        {
            // Modern styling
            BackColor = Color.FromArgb(245, 248, 252);
            
            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 248, 252),
                Padding = new Padding(12)
            };
            Controls.Add(_mainPanel);

            CreateControlsPanel();
            CreateChartsPanel();
        }

        private void CreateControlsPanel()
        {
            _controlsPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(230, 240, 250),
                Padding = new Padding(8)
            };
            
            // Add rounded corners
            _controlsPanel.Paint += (sender, e) => DrawRoundedPanel(e.Graphics, _controlsPanel.ClientRectangle, 8, Color.FromArgb(230, 240, 250));

            // Enable analysis checkbox
            _enableAnalysisCheckBox = new CheckBox
            {
                Text = "Enable Real-time Analysis",
                Location = new Point(12, 15),
                Size = new Size(180, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 70, 80),
                BackColor = Color.Transparent,
                Checked = _config.EnableRealTimeAnalysis
            };
            _enableAnalysisCheckBox.CheckedChanged += OnEnableAnalysisChanged;

            // Analysis window selector
            var windowLabel = new Label
            {
                Text = "Window:",
                Location = new Point(200, 17),
                Size = new Size(60, 16),
                Font = new Font("Segoe UI", 8.25F),
                ForeColor = Color.FromArgb(80, 90, 100),
                BackColor = Color.Transparent
            };

            _analysisWindowComboBox = new ComboBox
            {
                Location = new Point(265, 14),
                Size = new Size(80, 23),
                Font = new Font("Segoe UI", 8.25F),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _analysisWindowComboBox.Items.AddRange(new object[]
            {
                "10s", "30s", "1m", "2m", "5m"
            });
            _analysisWindowComboBox.SelectedIndex = 1; // 30s default
            _analysisWindowComboBox.SelectedIndexChanged += OnAnalysisWindowChanged;

            // Status label
            _statusLabel = new Label
            {
                Text = "Analysis disabled",
                Location = new Point(360, 17),
                Size = new Size(200, 16),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Italic),
                ForeColor = Color.FromArgb(120, 130, 140),
                BackColor = Color.Transparent
            };

            _controlsPanel.Controls.AddRange(new Control[]
            {
                _enableAnalysisCheckBox, windowLabel, _analysisWindowComboBox, _statusLabel
            });

            _mainPanel.Controls.Add(_controlsPanel);
        }

        private void CreateChartsPanel()
        {
            _chartsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 8, 0, 0)
            };

            // Create a 2x2 grid layout for charts
            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding = new Padding(4)
            };

            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            // Frequency Activity Panel
            _frequencyPanel = CreateChartPanel("Frequency Activity", Color.FromArgb(100, 150, 255));
            _frequencyPanel.Paint += OnFrequencyPanelPaint;

            // Player Activity Panel
            _playerPanel = CreateChartPanel("Player Activity", Color.FromArgb(255, 150, 100));
            _playerPanel.Paint += OnPlayerPanelPaint;

            // Modulation Activity Panel
            _modulationPanel = CreateChartPanel("Modulation Activity", Color.FromArgb(150, 255, 100));
            _modulationPanel.Paint += OnModulationPanelPaint;

            // Statistics Panel
            _statisticsPanel = CreateChartPanel("Statistics", Color.FromArgb(255, 200, 100));
            _statisticsPanel.Paint += OnStatisticsPanelPaint;

            // Add panels to table layout
            tableLayout.Controls.Add(_frequencyPanel, 0, 0);
            tableLayout.Controls.Add(_playerPanel, 1, 0);
            tableLayout.Controls.Add(_modulationPanel, 0, 1);
            tableLayout.Controls.Add(_statisticsPanel, 1, 1);

            _chartsPanel.Controls.Add(tableLayout);
            _mainPanel.Controls.Add(_chartsPanel);
        }

        private Panel CreateChartPanel(string title, Color accentColor)
        {
            var panel = new Panel
            {
                BackColor = Color.FromArgb(250, 252, 255),
                Margin = new Padding(4),
                Tag = new { Title = title, AccentColor = accentColor }
            };

            // Add rounded border
            panel.Paint += (sender, e) =>
            {
                DrawRoundedPanel(e.Graphics, panel.ClientRectangle, 8, panel.BackColor);
                DrawPanelBorder(e.Graphics, panel.ClientRectangle, 8, accentColor);
            };

            return panel;
        }

        private void SetupTimer()
        {
            _updateTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000, // Update every second
                Enabled = false
            };
            _updateTimer.Tick += OnUpdateTimerTick;
        }

        #endregion

        #region Event Handlers

        private void OnEnableAnalysisChanged(object? sender, EventArgs e)
        {
            IsAnalysisEnabled = _enableAnalysisCheckBox.Checked;
            
            if (IsAnalysisEnabled)
            {
                _updateTimer.Start();
                _statusLabel.Text = "Analysis active";
                _statusLabel.ForeColor = Color.FromArgb(60, 120, 60);
            }
            else
            {
                _updateTimer.Stop();
                _statusLabel.Text = "Analysis disabled";
                _statusLabel.ForeColor = Color.FromArgb(120, 130, 140);
            }

            InvalidateCharts();
        }

        private void OnAnalysisWindowChanged(object? sender, EventArgs e)
        {
            var selectedWindow = _analysisWindowComboBox.SelectedItem?.ToString();
            var timeSpan = selectedWindow switch
            {
                "10s" => TimeSpan.FromSeconds(10),
                "30s" => TimeSpan.FromSeconds(30),
                "1m" => TimeSpan.FromMinutes(1),
                "2m" => TimeSpan.FromMinutes(2),
                "5m" => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromSeconds(30)
            };

            Config = _config with { AnalysisWindow = timeSpan };
        }

        private void OnUpdateTimerTick(object? sender, EventArgs e)
        {
            if (IsAnalysisEnabled)
            {
                InvalidateCharts();
                
                // Update status
                var packetsPerSecond = _currentStats.AveragePacketsPerSecond;
                _statusLabel.Text = $"Analysis active - {packetsPerSecond:F1} packets/sec";
            }
        }

        #endregion

        #region Chart Drawing

        private void OnFrequencyPanelPaint(object? sender, PaintEventArgs e)
        {
            if (!IsAnalysisEnabled) return;

            var panel = (Panel)sender!;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var title = "Frequency Activity";
            var accentColor = Color.FromArgb(100, 150, 255);
            
            DrawChartTitle(g, panel.ClientRectangle, title, accentColor);
            
            if (_currentStats.FrequencyActivity.Any())
            {
                DrawFrequencyChart(g, panel.ClientRectangle, _currentStats.FrequencyActivity, accentColor);
            }
            else
            {
                DrawNoDataMessage(g, panel.ClientRectangle);
            }
        }

        private void OnPlayerPanelPaint(object? sender, PaintEventArgs e)
        {
            if (!IsAnalysisEnabled) return;

            var panel = (Panel)sender!;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var title = "Player Activity";
            var accentColor = Color.FromArgb(255, 150, 100);
            
            DrawChartTitle(g, panel.ClientRectangle, title, accentColor);
            
            if (_currentStats.PlayerActivity.Any())
            {
                DrawPlayerChart(g, panel.ClientRectangle, _currentStats.PlayerActivity, accentColor);
            }
            else
            {
                DrawNoDataMessage(g, panel.ClientRectangle);
            }
        }

        private void OnModulationPanelPaint(object? sender, PaintEventArgs e)
        {
            if (!IsAnalysisEnabled) return;

            var panel = (Panel)sender!;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var title = "Modulation Activity";
            var accentColor = Color.FromArgb(150, 255, 100);
            
            DrawChartTitle(g, panel.ClientRectangle, title, accentColor);
            
            if (_currentStats.ModulationActivity.Any())
            {
                DrawModulationChart(g, panel.ClientRectangle, _currentStats.ModulationActivity, accentColor);
            }
            else
            {
                DrawNoDataMessage(g, panel.ClientRectangle);
            }
        }

        private void OnStatisticsPanelPaint(object? sender, PaintEventArgs e)
        {
            if (!IsAnalysisEnabled) return;

            var panel = (Panel)sender!;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var title = "Statistics";
            var accentColor = Color.FromArgb(255, 200, 100);
            
            DrawChartTitle(g, panel.ClientRectangle, title, accentColor);
            DrawStatistics(g, panel.ClientRectangle, _currentStats);
        }

        private void DrawChartTitle(Graphics g, Rectangle bounds, string title, Color accentColor)
        {
            var titleFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            var titleBrush = new SolidBrush(Color.FromArgb(60, 70, 80));
            var titleSize = g.MeasureString(title, titleFont);
            
            var titleRect = new PointF(
                bounds.X + 12,
                bounds.Y + 8
            );
            
            g.DrawString(title, titleFont, titleBrush, titleRect);
            
            // Draw accent line under title
            using (var accentPen = new Pen(accentColor, 2))
            {
                var lineY = bounds.Y + 8 + titleSize.Height + 2;
                g.DrawLine(accentPen, bounds.X + 12, lineY, bounds.X + 12 + titleSize.Width, lineY);
            }
        }

        private void DrawFrequencyChart(Graphics g, Rectangle bounds, Dictionary<double, int> frequencyData, Color accentColor)
        {
            var chartRect = new Rectangle(bounds.X + 15, bounds.Y + 40, bounds.Width - 30, bounds.Height - 60);
            
            if (chartRect.Width <= 0 || chartRect.Height <= 0) return;

            var sortedData = frequencyData.OrderByDescending(kvp => kvp.Value).Take(10).ToList();
            if (!sortedData.Any()) return;

            var maxValue = sortedData.Max(kvp => kvp.Value);
            var barHeight = Math.Max(1, chartRect.Height / sortedData.Count - 2);
            
            for (int i = 0; i < sortedData.Count; i++)
            {
                var data = sortedData[i];
                var barWidth = (int)((float)data.Value / maxValue * chartRect.Width * 0.8f);
                var barRect = new Rectangle(
                    chartRect.X,
                    chartRect.Y + i * (barHeight + 2),
                    barWidth,
                    barHeight
                );
                
                using (var brush = new SolidBrush(Color.FromArgb(150, accentColor)))
                {
                    g.FillRectangle(brush, barRect);
                }
                
                // Draw frequency label
                var font = new Font("Segoe UI", 7F);
                var text = $"{data.Key:F1} MHz ({data.Value})";
                var textRect = new PointF(chartRect.X + barWidth + 4, barRect.Y);
                using (var textBrush = new SolidBrush(Color.FromArgb(80, 90, 100)))
                {
                    g.DrawString(text, font, textBrush, textRect);
                }
            }
        }

        private void DrawPlayerChart(Graphics g, Rectangle bounds, Dictionary<string, int> playerData, Color accentColor)
        {
            var chartRect = new Rectangle(bounds.X + 15, bounds.Y + 40, bounds.Width - 30, bounds.Height - 60);
            
            if (chartRect.Width <= 0 || chartRect.Height <= 0) return;

            var sortedData = playerData.OrderByDescending(kvp => kvp.Value).Take(8).ToList();
            if (!sortedData.Any()) return;

            var maxValue = sortedData.Max(kvp => kvp.Value);
            var barHeight = Math.Max(1, chartRect.Height / sortedData.Count - 2);
            
            for (int i = 0; i < sortedData.Count; i++)
            {
                var data = sortedData[i];
                var barWidth = (int)((float)data.Value / maxValue * chartRect.Width * 0.8f);
                var barRect = new Rectangle(
                    chartRect.X,
                    chartRect.Y + i * (barHeight + 2),
                    barWidth,
                    barHeight
                );
                
                using (var brush = new SolidBrush(Color.FromArgb(150, accentColor)))
                {
                    g.FillRectangle(brush, barRect);
                }
                
                // Draw player label
                var font = new Font("Segoe UI", 7F);
                var playerName = data.Key.Length > 15 ? data.Key.Substring(0, 12) + "..." : data.Key;
                var text = $"{playerName} ({data.Value})";
                var textRect = new PointF(chartRect.X + barWidth + 4, barRect.Y);
                using (var textBrush = new SolidBrush(Color.FromArgb(80, 90, 100)))
                {
                    g.DrawString(text, font, textBrush, textRect);
                }
            }
        }

        private void DrawModulationChart(Graphics g, Rectangle bounds, Dictionary<string, int> modulationData, Color accentColor)
        {
            var chartRect = new Rectangle(bounds.X + 15, bounds.Y + 40, bounds.Width - 30, bounds.Height - 60);
            
            if (chartRect.Width <= 0 || chartRect.Height <= 0 || !modulationData.Any()) return;

            // Draw pie chart for modulations
            var total = modulationData.Values.Sum();
            if (total == 0) return;

            var centerX = chartRect.X + chartRect.Width / 2;
            var centerY = chartRect.Y + chartRect.Height / 2;
            var radius = Math.Min(chartRect.Width, chartRect.Height) / 3;
            
            var startAngle = 0f;
            var colorIndex = 0;
            
            foreach (var data in modulationData.OrderByDescending(kvp => kvp.Value))
            {
                var sweepAngle = (float)data.Value / total * 360f;
                var pieRect = new Rectangle(centerX - radius, centerY - radius, radius * 2, radius * 2);
                
                var color = _chartColors[colorIndex % _chartColors.Length];
                using (var brush = new SolidBrush(Color.FromArgb(150, color)))
                {
                    g.FillPie(brush, pieRect, startAngle, sweepAngle);
                }
                
                // Draw percentage label
                if (sweepAngle > 15) // Only show label if slice is large enough
                {
                    var labelAngle = (startAngle + sweepAngle / 2) * Math.PI / 180;
                    var labelX = centerX + (float)(Math.Cos(labelAngle) * radius * 0.7);
                    var labelY = centerY + (float)(Math.Sin(labelAngle) * radius * 0.7);
                    
                    var percentage = (float)data.Value / total * 100;
                    var text = $"{percentage:F0}%";
                    var font = new Font("Segoe UI", 7F, FontStyle.Bold);
                    var size = g.MeasureString(text, font);
                    
                    using (var textBrush = new SolidBrush(Color.White))
                    {
                        g.DrawString(text, font, textBrush, labelX - size.Width / 2, labelY - size.Height / 2);
                    }
                }
                
                startAngle += sweepAngle;
                colorIndex++;
            }
            
            // Draw legend
            var legendY = chartRect.Bottom - 60;
            var legendX = chartRect.X + 10;
            colorIndex = 0;
            
            foreach (var data in modulationData.OrderByDescending(kvp => kvp.Value).Take(4))
            {
                var color = _chartColors[colorIndex % _chartColors.Length];
                var legendRect = new Rectangle(legendX, legendY + colorIndex * 12, 8, 8);
                
                using (var brush = new SolidBrush(color))
                {
                    g.FillRectangle(brush, legendRect);
                }
                
                var font = new Font("Segoe UI", 7F);
                var text = $"{data.Key} ({data.Value})";
                using (var textBrush = new SolidBrush(Color.FromArgb(80, 90, 100)))
                {
                    g.DrawString(text, font, textBrush, legendX + 12, legendY + colorIndex * 12 - 1);
                }
                
                colorIndex++;
            }
        }

        private void DrawStatistics(Graphics g, Rectangle bounds, LiveAnalysisStats stats)
        {
            var font = new Font("Segoe UI", 8.25F);
            var labelFont = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            var textBrush = new SolidBrush(Color.FromArgb(60, 70, 80));
            var labelBrush = new SolidBrush(Color.FromArgb(80, 90, 100));
            
            var y = bounds.Y + 45;
            var lineHeight = 16;
            var labelX = bounds.X + 15;
            var valueX = bounds.X + 120;
            
            // Draw statistics
            var statistics = new[]
            {
                ("Processed Packets:", stats.ProcessedPackets.ToString()),
                ("Analysis Duration:", stats.AnalysisDuration.ToString(@"mm\:ss")),
                ("Avg. Packets/sec:", $"{stats.AveragePacketsPerSecond:F1}"),
                ("Active Frequencies:", stats.FrequencyActivity.Count.ToString()),
                ("Active Players:", stats.PlayerActivity.Count.ToString()),
                ("Active Modulations:", stats.ModulationActivity.Count.ToString())
            };
            
            foreach (var (label, value) in statistics)
            {
                g.DrawString(label, labelFont, labelBrush, labelX, y);
                g.DrawString(value, font, textBrush, valueX, y);
                y += lineHeight;
            }
        }

        private void DrawNoDataMessage(Graphics g, Rectangle bounds)
        {
            var font = new Font("Segoe UI", 8.25F, FontStyle.Italic);
            var brush = new SolidBrush(Color.FromArgb(120, 130, 140));
            var text = "No data available";
            var size = g.MeasureString(text, font);
            
            var x = bounds.X + (bounds.Width - size.Width) / 2;
            var y = bounds.Y + (bounds.Height - size.Height) / 2;
            
            g.DrawString(text, font, brush, x, y);
        }

        #endregion

        #region Helper Methods

        private void UpdateConfigControls()
        {
            _enableAnalysisCheckBox.Checked = _config.EnableRealTimeAnalysis;
            
            var windowText = _config.AnalysisWindow.TotalSeconds switch
            {
                10 => "10s",
                30 => "30s",
                60 => "1m",
                120 => "2m",
                300 => "5m",
                _ => "30s"
            };
            
            _analysisWindowComboBox.SelectedItem = windowText;
        }

        private void InvalidateCharts()
        {
            _frequencyPanel?.Invalidate();
            _playerPanel?.Invalidate();
            _modulationPanel?.Invalidate();
            _statisticsPanel?.Invalidate();
        }

        private void DrawRoundedPanel(Graphics g, Rectangle bounds, int cornerRadius, Color fillColor)
        {
            using (var path = CreateRoundedRectanglePath(bounds, cornerRadius))
            using (var brush = new SolidBrush(fillColor))
            {
                g.FillPath(brush, path);
            }
        }

        private void DrawPanelBorder(Graphics g, Rectangle bounds, int cornerRadius, Color borderColor)
        {
            using (var path = CreateRoundedRectanglePath(bounds, cornerRadius))
            using (var pen = new Pen(Color.FromArgb(100, borderColor), 1))
            {
                g.DrawPath(pen, path);
            }
        }

        private GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int cornerRadius)
        {
            var path = new GraphicsPath();
            var diameter = cornerRadius * 2;

            if (diameter >= rect.Width || diameter >= rect.Height)
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

        #endregion

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion

        private void InitializeComponent()
        {
            SuspendLayout();
            
            Name = "LiveAnalysisComponent";
            Size = new Size(600, 400);
            
            ResumeLayout(false);
        }
    }
}