using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Extensions;
using ShalevOhad.DCS.SRS.Recorder.Core.Analysis;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Components
{
    /// <summary>
    /// Component for file analysis functionality including activity analysis and frequency analysis
    /// </summary>
    public partial class AnalyzerComponent : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Services
        
        private IUIService? _uiService;
        
        #endregion

        #region Controls
        
        private Panel _mainPanel;
        private Panel _controlsPanel;
        private Panel _resultsPanel;
        private Splitter _resultsSplitter;
        
        // Analysis controls
        private Button _analyzeActivityButton;
        private Button _analyzeFrequenciesButton;
        private NumericUpDown _silenceThresholdNumericUpDown;
        private NumericUpDown _minimumActivityNumericUpDown;
        private ProgressBar _analysisProgressBar;
        private Label _analysisStatusLabel;
        
        // Results display
        private TreeView _frequencyTreeView;
        private RichTextBox _analysisResultsTextBox;
        
        #endregion

        #region State
        
        private string? _currentFilePath;
        private List<FrequencyModulationInfo> _currentFrequencies = new();
        
        #endregion

        #region Events
        
        public event EventHandler<string>? StatusChanged;
        public event EventHandler? AnalysisStarted;
        public event EventHandler<string>? AnalysisCompleted;
        
        #endregion

        public AnalyzerComponent()
        {
            Logger.Info("AnalyzerComponent constructor called");
            InitializeComponent();
            CreateControls();
            SetupEventHandlers();
            Logger.Info("AnalyzerComponent constructor completed");
        }

        public void Initialize(IUIService uiService)
        {
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
        }

        public async Task LoadFileAsync(string filePath, RecordingFileInfo recordingInfo)
        {
            try
            {
                _currentFilePath = filePath;
                
                // Enable analysis controls
                _analyzeActivityButton.Enabled = true;
                _analyzeFrequenciesButton.Enabled = true;
                
                // Update status
                _analysisStatusLabel.Text = "File ready for analysis";
                _analysisStatusLabel.ForeColor = Color.Green;
                
                OnStatusChanged($"Analyzer ready: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading file in analyzer component");
                _uiService?.ShowError($"Error loading file: {ex.Message}");
            }
        }

        #region Control Creation

        private void CreateControls()
        {
            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8)
            };
            Controls.Add(_mainPanel);

            // Create controls in proper docking order
            // Top controls first, Fill controls last
            CreateControlsPanel();  // DockStyle.Top - add first
            CreateResultsPanel();   // DockStyle.Fill - add last
        }

        private void CreateControlsPanel()
        {
            _controlsPanel = new Panel
            {
                Height = 100,
                Dock = DockStyle.Top,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(12)
            };

            // Create organized layout
            var parametersPanel = new Panel { Height = 30, Dock = DockStyle.Top };
            var buttonsPanel = new Panel { Height = 35, Dock = DockStyle.Top };
            var statusPanel = new Panel { Dock = DockStyle.Fill };

            // Add controls in proper docking order - Top controls first, Fill control last
            _controlsPanel.Controls.Add(parametersPanel);  // DockStyle.Top - add first
            _controlsPanel.Controls.Add(buttonsPanel);     // DockStyle.Top - add second
            _controlsPanel.Controls.Add(statusPanel);      // DockStyle.Fill - add last

            CreateParameterControls(parametersPanel);
            CreateAnalysisButtons(buttonsPanel);
            CreateStatusLabel(statusPanel);

            _mainPanel.Controls.Add(_controlsPanel);
        }

        private void CreateParameterControls(Panel panel)
        {
            var flowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false
            };
            panel.Controls.Add(flowLayout);

            // Silence threshold
            var silenceLabel = new Label
            {
                Text = "Silence Threshold:",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 8, 8, 0)
            };
            flowLayout.Controls.Add(silenceLabel);

            _silenceThresholdNumericUpDown = new NumericUpDown
            {
                Minimum = 100,
                Maximum = 5000,
                Value = 500,
                Increment = 100,
                Width = 80,
                Margin = new Padding(0, 6, 16, 0)
            };
            flowLayout.Controls.Add(_silenceThresholdNumericUpDown);

            // Minimum activity
            var activityLabel = new Label
            {
                Text = "Min Activity (ms):",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 8, 8, 0)
            };
            flowLayout.Controls.Add(activityLabel);

            _minimumActivityNumericUpDown = new NumericUpDown
            {
                Minimum = 50,
                Maximum = 2000,
                Value = 100,
                Increment = 50,
                Width = 80,
                Margin = new Padding(0, 6, 0, 0)
            };
            flowLayout.Controls.Add(_minimumActivityNumericUpDown);
        }

        private void CreateAnalysisButtons(Panel panel)
        {
            var flowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false
            };
            panel.Controls.Add(flowLayout);

            _analyzeActivityButton = new Button
            {
                Text = "Analyze Activity",
                Size = new Size(120, 32),
                Margin = new Padding(0, 0, 8, 0),
                Enabled = false
            };
            flowLayout.Controls.Add(_analyzeActivityButton);

            _analyzeFrequenciesButton = new Button
            {
                Text = "Analyze Frequencies",
                Size = new Size(130, 32),
                Margin = new Padding(0, 0, 16, 0),
                Enabled = false
            };
            flowLayout.Controls.Add(_analyzeFrequenciesButton);

            _analysisProgressBar = new ProgressBar
            {
                Size = new Size(150, 28),
                Visible = false,
                Margin = new Padding(0, 2, 0, 0)
            };
            flowLayout.Controls.Add(_analysisProgressBar);
        }

        private void CreateStatusLabel(Panel panel)
        {
            _analysisStatusLabel = new Label
            {
                Text = "Select a file to analyze",
                Dock = DockStyle.Fill,
                ForeColor = SystemColors.ControlDarkDark,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 8, 0, 0)
            };
            panel.Controls.Add(_analysisStatusLabel);
        }

        private void CreateResultsPanel()
        {
            _resultsPanel = new Panel
            {
                Dock = DockStyle.Fill
            };
            _mainPanel.Controls.Add(_resultsPanel);

            // Create TreeView for frequency analysis results
            _frequencyTreeView = new TreeView
            {
                Dock = DockStyle.Left,
                Width = 350,
                CheckBoxes = true,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                FullRowSelect = true,
                HideSelection = false,
                Font = new Font("Segoe UI", 9F),
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            // Create splitter
            _resultsSplitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 4,
                BackColor = SystemColors.ControlDark,
                Visible = false
            };

            // Create RichTextBox for text results
            _analysisResultsTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9F),
                Text = GetInitialHelpText(),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Add controls to results panel (order matters for docking)
            // Controls with DockStyle.Fill should be added last
            _resultsPanel.Controls.Add(_frequencyTreeView);    // DockStyle.Left - add first
            _resultsPanel.Controls.Add(_resultsSplitter);      // DockStyle.Left - add second
            _resultsPanel.Controls.Add(_analysisResultsTextBox); // DockStyle.Fill - add last
        }

        #endregion

        #region Event Handlers

        private void SetupEventHandlers()
        {
            _analyzeActivityButton.Click += OnAnalyzeActivity;
            _analyzeFrequenciesButton.Click += OnAnalyzeFrequencies;
            _frequencyTreeView.AfterCheck += OnTreeViewAfterCheck;
            _frequencyTreeView.NodeMouseDoubleClick += OnTreeViewNodeDoubleClick;
        }

        private async void OnAnalyzeActivity(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                _uiService?.ShowWarning("Please select a recording file first.");
                return;
            }

            try
            {
                SetAnalysisInProgress(true);
                AnalysisStarted?.Invoke(this, EventArgs.Empty);
                
                _analysisStatusLabel.Text = "Analyzing activity...";
                _analysisStatusLabel.ForeColor = Color.Blue;

                AppendToResults($"\n[{DateTime.Now:HH:mm:ss}] Starting audio activity analysis...");
                AppendToResults($"File: {Path.GetFileName(_currentFilePath)}");
                AppendToResults($"Silence Threshold: {_silenceThresholdNumericUpDown.Value}");
                AppendToResults($"Minimum Activity Duration: {_minimumActivityNumericUpDown.Value}ms");

                var silenceThreshold = (int)_silenceThresholdNumericUpDown.Value;
                var minimumActivity = TimeSpan.FromMilliseconds((double)_minimumActivityNumericUpDown.Value);

                var analysis = await Task.Run(() =>
                    FileAnalyzer.AnalyzeAudioActivity(
                        _currentFilePath,
                        silenceThreshold,
                        minimumActivity));

                var results = analysis.ToString();
                AppendToResults($"\n{results}");
                
                _analysisStatusLabel.Text = "Activity analysis completed";
                _analysisStatusLabel.ForeColor = Color.Green;
                
                AnalysisCompleted?.Invoke(this, results);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error analyzing audio activity");
                AppendToResults($"\n[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                _analysisStatusLabel.Text = "Analysis failed";
                _analysisStatusLabel.ForeColor = Color.Red;
                _uiService?.ShowError($"Analysis failed: {ex.Message}");
            }
            finally
            {
                SetAnalysisInProgress(false);
            }
        }

        private async void OnAnalyzeFrequencies(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                _uiService?.ShowWarning("Please select a recording file first.");
                return;
            }

            try
            {
                SetAnalysisInProgress(true);
                AnalysisStarted?.Invoke(this, EventArgs.Empty);
                
                _analysisStatusLabel.Text = "Analyzing frequencies...";
                _analysisStatusLabel.ForeColor = Color.Blue;

                AppendToResults($"\n[{DateTime.Now:HH:mm:ss}] Starting frequency analysis...");
                AppendToResults($"File: {Path.GetFileName(_currentFilePath)}");

                var frequencies = await Task.Run(() =>
                    FileAnalyzer.GetAllFrequencyModulations(_currentFilePath));

                DisplayFrequencyResults(frequencies);

                _analysisStatusLabel.Text = "Frequency analysis completed";
                _analysisStatusLabel.ForeColor = Color.Green;
                
                AnalysisCompleted?.Invoke(this, $"Found {frequencies.Count} frequency-modulation combinations");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error analyzing frequencies");
                AppendToResults($"\n[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                _analysisStatusLabel.Text = "Analysis failed";
                _analysisStatusLabel.ForeColor = Color.Red;
                _uiService?.ShowError($"Analysis failed: {ex.Message}");
            }
            finally
            {
                SetAnalysisInProgress(false);
            }
        }

        private void OnTreeViewAfterCheck(object? sender, TreeViewEventArgs e)
        {
            try
            {
                if (e.Node?.Tag == null) return;

                var nodeData = e.Node.Tag;
                var nodeType = nodeData.GetType().GetProperty("Type")?.GetValue(nodeData)?.ToString();

                if (nodeType == "Coalition")
                {
                    // Check/uncheck all frequency nodes under this coalition
                    foreach (TreeNode freqNode in e.Node.Nodes)
                    {
                        freqNode.Checked = e.Node.Checked;
                    }
                }

                UpdateFrequencySelection();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling tree view check change");
            }
        }

        private void OnTreeViewNodeDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            try
            {
                if (e.Node?.Tag == null) return;

                var nodeData = e.Node.Tag;
                var nodeType = nodeData.GetType().GetProperty("Type")?.GetValue(nodeData)?.ToString();

                switch (nodeType)
                {
                    case "Coalition":
                        ShowCoalitionDetails(nodeData);
                        break;
                    case "Frequency":
                        ShowFrequencyDetails(nodeData);
                        break;
                    case "Player":
                        ShowPlayerDetails(nodeData);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling tree view double click");
            }
        }

        #endregion

        #region Analysis Helper Methods

        private void SetAnalysisInProgress(bool inProgress)
        {
            _analyzeActivityButton.Enabled = !inProgress && !string.IsNullOrEmpty(_currentFilePath);
            _analyzeFrequenciesButton.Enabled = !inProgress && !string.IsNullOrEmpty(_currentFilePath);
            _analysisProgressBar.Visible = inProgress;
            _analysisProgressBar.Style = inProgress ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        }

        private void AppendToResults(string message)
        {
            _uiService?.InvokeOnUIThread(() =>
            {
                _analysisResultsTextBox.AppendText(message + Environment.NewLine);
                _analysisResultsTextBox.ScrollToCaret();
            });
        }

        private void DisplayFrequencyResults(List<FrequencyModulationInfo> frequencies)
        {
            _currentFrequencies = frequencies;

            // Show TreeView and splitter
            _frequencyTreeView.Visible = true;
            _resultsSplitter.Visible = true;

            // Populate TreeView
            PopulateFrequencyTreeView(frequencies);

            AppendToResults($"\n=== Frequency Analysis Results ===");
            AppendToResults($"Total unique frequency-modulation combinations: {frequencies.Count}");
            AppendToResults($"Use the tree view on the left to explore frequencies by coalition.");
            AppendToResults($"Double-click on any item for detailed information.");
        }

        private void PopulateFrequencyTreeView(List<FrequencyModulationInfo> frequencies)
        {
            _frequencyTreeView.BeginUpdate();
            _frequencyTreeView.Nodes.Clear();

            if (!frequencies.Any())
            {
                var noDataNode = new TreeNode("No frequency data available")
                {
                    ForeColor = SystemColors.GrayText
                };
                _frequencyTreeView.Nodes.Add(noDataNode);
                _frequencyTreeView.EndUpdate();
                return;
            }

            // Group frequencies by coalition
            var coalitionGroups = frequencies
                .SelectMany(f => f.Players.Select(p => new { Frequency = f, Player = p }))
                .GroupBy(x => x.Player.Coalition?.ToString() ?? "Unknown")
                .OrderBy(g => g.Key);

            foreach (var coalitionGroup in coalitionGroups)
            {
                var coalitionName = GetCoalitionName(coalitionGroup.Key);
                var totalFrequencies = coalitionGroup.GroupBy(x => x.Frequency).Count();
                var totalPlayers = coalitionGroup.Select(x => x.Player).DistinctBy(p => p.Name).Count();
                
                var coalitionNode = new TreeNode($"{coalitionName} ({totalFrequencies} freq, {totalPlayers} players)")
                {
                    Tag = new { Type = "Coalition", Name = coalitionName, Data = coalitionGroup.ToList() },
                    ForeColor = GetCoalitionColor(coalitionName),
                    NodeFont = new Font(_frequencyTreeView.Font, FontStyle.Bold),
                    Checked = true
                };

                var frequencyGroups = coalitionGroup
                    .GroupBy(x => x.Frequency)
                    .OrderBy(fg => fg.Key.Frequency);

                foreach (var freqGroup in frequencyGroups)
                {
                    var freq = freqGroup.Key;
                    var players = freqGroup.Select(x => x.Player).Distinct().ToList();
                    
                    var freqNode = new TreeNode($"{freq.Frequency:F1} MHz ({freq.GetModulationName()}) - {players.Count} users")
                    {
                        Tag = new { Type = "Frequency", Frequency = freq, Players = players },
                        Checked = true,
                        ForeColor = SystemColors.WindowText
                    };

                    // Add players as child nodes
                    foreach (var player in players.OrderBy(p => p.Name))
                    {
                        var duration = player.LastSeen - player.FirstSeen;
                        var playerText = $"{player.Name} ({player.Aircraft}) - {player.PacketCount:N0} packets";
                        
                        var playerNode = new TreeNode(playerText)
                        {
                            Tag = new { Type = "Player", Player = player, Frequency = freq },
                            ForeColor = SystemColors.GrayText,
                            NodeFont = new Font(_frequencyTreeView.Font, FontStyle.Regular)
                        };

                        freqNode.Nodes.Add(playerNode);
                    }

                    coalitionNode.Nodes.Add(freqNode);
                }

                _frequencyTreeView.Nodes.Add(coalitionNode);
            }

            // Expand all nodes by default
            _frequencyTreeView.ExpandAll();
            _frequencyTreeView.EndUpdate();
        }

        private void UpdateFrequencySelection()
        {
            var selectedFrequencies = new List<dynamic>();
            
            foreach (TreeNode coalitionNode in _frequencyTreeView.Nodes)
            {
                foreach (TreeNode freqNode in coalitionNode.Nodes)
                {
                    if (freqNode.Checked && freqNode.Tag != null)
                    {
                        var frequency = freqNode.Tag.GetType().GetProperty("Frequency")?.GetValue(freqNode.Tag);
                        if (frequency != null)
                            selectedFrequencies.Add(frequency);
                    }
                }
            }

            AppendToResults($"\n[{DateTime.Now:HH:mm:ss}] Selection updated: {selectedFrequencies.Count} frequencies selected");
            OnStatusChanged($"{selectedFrequencies.Count} frequencies selected for detailed analysis");
        }

        private void ShowCoalitionDetails(dynamic nodeData)
        {
            try
            {
                var coalitionName = nodeData.GetType().GetProperty("Name")?.GetValue(nodeData)?.ToString();
                var data = nodeData.GetType().GetProperty("Data")?.GetValue(nodeData) as IEnumerable<dynamic>;

                if (data == null || string.IsNullOrEmpty(coalitionName)) return;

                var message = $"Coalition: {coalitionName}\n\n";
                
                var totalPlayers = data.Select(x => x.Player).DistinctBy(p => p.Name).Count();
                var totalFrequencies = data.GroupBy(x => x.Frequency).Count();
                var totalPackets = data.Select(x => x.Player).Sum(p => p.PacketCount);
                
                message += $"Total Players: {totalPlayers}\n";
                message += $"Total Frequencies: {totalFrequencies}\n";
                message += $"Total Packets: {totalPackets:N0}\n\n";
                message += "Frequency Breakdown:\n";
                
                var freqGroups = data.GroupBy(x => x.Frequency).OrderBy(g => g.Key.Frequency);
                foreach (var freqGroup in freqGroups)
                {
                    var freq = freqGroup.Key;
                    var players = freqGroup.Select(x => x.Player).Distinct().Count();
                    var packets = freqGroup.Select(x => x.Player).Sum(p => p.PacketCount);
                    message += $"• {freq.Frequency:F1} MHz ({freq.GetModulationName()}): {players} players, {packets:N0} packets\n";
                }

                _uiService?.ShowInfo(message, $"Coalition Details - {coalitionName}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing coalition details");
            }
        }

        private void ShowFrequencyDetails(dynamic nodeData)
        {
            try
            {
                var frequency = nodeData.GetType().GetProperty("Frequency")?.GetValue(nodeData);
                var players = nodeData.GetType().GetProperty("Players")?.GetValue(nodeData);

                if (frequency == null || players == null) return;

                var freqValue = frequency.GetType().GetProperty("Frequency")?.GetValue(frequency);
                var modulation = frequency.GetType().GetProperty("Modulation")?.GetValue(frequency);
                
                var message = $"Frequency: {freqValue:F1} MHz ({modulation})\n\n";
                message += "Players on this frequency:\n";
                
                var playersList = players as System.Collections.IEnumerable;
                if (playersList != null)
                {
                    foreach (var player in playersList)
                    {
                        var name = player.GetType().GetProperty("Name")?.GetValue(player);
                        var aircraft = player.GetType().GetProperty("Aircraft")?.GetValue(player);
                        var coalition = player.GetType().GetProperty("Coalition")?.GetValue(player);
                        var packetCount = player.GetType().GetProperty("PacketCount")?.GetValue(player);
                        
                        var coalitionName = GetCoalitionName(coalition?.ToString() ?? "Unknown");
                        message += $"• {name} ({coalitionName}) - {aircraft}: {packetCount:N0} packets\n";
                    }
                }

                _uiService?.ShowInfo(message, $"Frequency Details - {freqValue:F1} MHz");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing frequency details");
            }
        }

        private void ShowPlayerDetails(dynamic nodeData)
        {
            try
            {
                var player = nodeData.GetType().GetProperty("Player")?.GetValue(nodeData);
                var frequency = nodeData.GetType().GetProperty("Frequency")?.GetValue(nodeData);

                if (player == null) return;

                var name = player.GetType().GetProperty("Name")?.GetValue(player);
                var aircraft = player.GetType().GetProperty("Aircraft")?.GetValue(player);
                var coalition = player.GetType().GetProperty("Coalition")?.GetValue(player);
                var packetCount = player.GetType().GetProperty("PacketCount")?.GetValue(player);
                var firstSeen = player.GetType().GetProperty("FirstSeen")?.GetValue(player);
                var lastSeen = player.GetType().GetProperty("LastSeen")?.GetValue(player);
                
                var message = $"Player: {name}\n";
                message += $"Coalition: {GetCoalitionName(coalition?.ToString() ?? "Unknown")}\n";
                message += $"Aircraft: {aircraft}\n\n";
                
                if (frequency != null)
                {
                    var freqValue = frequency.GetType().GetProperty("Frequency")?.GetValue(frequency);
                    var modulation = frequency.GetType().GetProperty("Modulation")?.GetValue(frequency);
                    message += $"Frequency: {freqValue:F1} MHz ({modulation})\n";
                }
                
                message += $"Total Packets: {packetCount:N0}\n";
                
                if (firstSeen is DateTime first && lastSeen is DateTime last)
                {
                    var duration = last - first;
                    message += $"\nActive Period:\n";
                    message += $"First Seen: {first:HH:mm:ss}\n";
                    message += $"Last Seen: {last:HH:mm:ss}\n";
                    message += $"Duration: {FormatDuration(duration)}\n";
                }

                _uiService?.ShowInfo(message, $"Player Details - {name}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing player details");
            }
        }

        #endregion

        #region Helper Methods

        private static string GetCoalitionName(string coalition)
        {
            return coalition switch
            {
                "1" => "Red",
                "2" => "Blue",
                _ => "Neutral"
            };
        }

        private static Color GetCoalitionColor(string coalition)
        {
            return coalition switch
            {
                "Red" => Color.DarkRed,
                "Blue" => Color.DarkBlue,
                _ => Color.DarkGreen
            };
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
            else if (duration.TotalMinutes >= 1)
                return $"{duration.Minutes}m {duration.Seconds}s";
            else
                return $"{duration.Seconds}.{duration.Milliseconds:D3}s";
        }

        private string GetInitialHelpText()
        {
            return "Analysis Results will appear here...\n\n" +
                   "This tool allows you to:\n" +
                   "• Analyze audio activity periods in recordings\n" +
                   "• Identify frequency usage patterns\n" +
                   "• Review player activity statistics\n" +
                   "• Export analysis results\n\n" +
                   "Select a recording file and choose an analysis type to begin.";
        }

        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        #endregion

        private void InitializeComponent()
        {
            SuspendLayout();
            
            Name = "AnalyzerComponent";
            Size = new Size(600, 400);
            
            ResumeLayout(false);
        }
    }
}