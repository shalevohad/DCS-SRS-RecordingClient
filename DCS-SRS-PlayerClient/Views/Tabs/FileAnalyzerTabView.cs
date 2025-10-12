using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.Core.Analysis;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Tabs
{
    /// <summary>
    /// Tab control for file analysis functionality
    /// </summary>
    public partial class FileAnalyzerTabView : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Services
        private IUIService? _uiService;
        private ISettingsService? _settingsService;

        // Controls
        private Panel _fileAnalyzerPanel;
        private Panel _analyzerFileInfoPanel;
        private Label _currentFileLabel;
        private Label _currentFilePathLabel;
        private Button _analyzeActivityButton;
        private Button _analyzeFrequenciesButton;
        private NumericUpDown _silenceThresholdNumericUpDown;
        private NumericUpDown _minimumActivityNumericUpDown;
        private RichTextBox _analysisResultsTextBox;
        private ProgressBar _analysisProgressBar;
        private Label _analysisStatusLabel;
        private TreeView _frequencyTreeView;
        private Panel _resultsContainerPanel;
        private Splitter _resultsSplitter;
        
        
        // Data storage
        private List<ShalevOhad.DCS.SRS.Recorder.Core.Models.FrequencyModulationInfo> _currentFrequencies = new();
        private string? _currentFilePath;
        private bool _suppressFileChangeEvents; // To prevent circular updates

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<string>? FileSelected;

        public FileAnalyzerTabView()
        {
            InitializeComponent();
            CreateControls();
            SetupEventHandlers();
        }

        public void Initialize(IUIService uiService, ISettingsService settingsService)
        {
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            
            // Load the current file path from settings if available
            var lastFilePath = _settingsService.LastFilePath;
            if (!string.IsNullOrEmpty(lastFilePath) && File.Exists(lastFilePath))
            {
                SetCurrentFile(lastFilePath);
            }
        }

        private void CreateControls()
        {
            _fileAnalyzerPanel = new Panel();
            _fileAnalyzerPanel.Dock = DockStyle.Fill;
            _fileAnalyzerPanel.Padding = new Padding(12);
            Controls.Add(_fileAnalyzerPanel);

            CreateFileInfoPanel();
            CreateAnalysisControlsPanel();
            CreateResultsTextBox();
        }

        private void CreateFileInfoPanel()
        {
            _analyzerFileInfoPanel = new Panel();
            _analyzerFileInfoPanel.Height = 50;
            _analyzerFileInfoPanel.Dock = DockStyle.Top;
            _analyzerFileInfoPanel.BorderStyle = BorderStyle.FixedSingle;
            _analyzerFileInfoPanel.Padding = new Padding(12);

            var fileInfoLayout = new TableLayoutPanel();
            fileInfoLayout.Dock = DockStyle.Fill;
            fileInfoLayout.ColumnCount = 2;
            fileInfoLayout.RowCount = 1;
            fileInfoLayout.Margin = new Padding(0);
            fileInfoLayout.Padding = new Padding(0);

            fileInfoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fileInfoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fileInfoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _analyzerFileInfoPanel.Controls.Add(fileInfoLayout);

            _currentFileLabel = new Label();
            _currentFileLabel.Text = "Current File:";
            _currentFileLabel.AutoSize = true;
            _currentFileLabel.Anchor = AnchorStyles.Left;
            _currentFileLabel.TextAlign = ContentAlignment.MiddleLeft;
            _currentFileLabel.Margin = new Padding(0, 0, 12, 0);
            _currentFileLabel.Font = new Font(_currentFileLabel.Font, FontStyle.Bold);
            fileInfoLayout.Controls.Add(_currentFileLabel, 0, 0);

            _currentFilePathLabel = new Label();
            _currentFilePathLabel.Text = "No file selected. Please use the Player tab to select a recording file.";
            _currentFilePathLabel.ForeColor = SystemColors.GrayText;
            _currentFilePathLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _currentFilePathLabel.TextAlign = ContentAlignment.MiddleLeft;
            _currentFilePathLabel.Margin = new Padding(0, 0, 0, 0);
            _currentFilePathLabel.UseMnemonic = false; // Prevent & from being treated as mnemonics
            fileInfoLayout.Controls.Add(_currentFilePathLabel, 1, 0);

            _fileAnalyzerPanel.Controls.Add(_analyzerFileInfoPanel);
        }

        private void CreateAnalysisControlsPanel()
        {
            var analysisControlsPanel = new Panel();
            analysisControlsPanel.Height = 100;
            analysisControlsPanel.Dock = DockStyle.Top;
            analysisControlsPanel.BorderStyle = BorderStyle.FixedSingle;
            analysisControlsPanel.Padding = new Padding(12);

            // Create organized layout with panels
            var parametersPanel = new Panel();
            parametersPanel.Height = 30;
            parametersPanel.Dock = DockStyle.Top;
            
            var buttonsPanel = new Panel();
            buttonsPanel.Height = 35;
            buttonsPanel.Dock = DockStyle.Top;
            
            var statusPanel = new Panel();
            statusPanel.Dock = DockStyle.Fill;

            analysisControlsPanel.Controls.Add(statusPanel);      // Fill first (bottom in dock order)
            analysisControlsPanel.Controls.Add(buttonsPanel);    // Top second
            analysisControlsPanel.Controls.Add(parametersPanel); // Top first

            CreateAnalysisParameterControls(parametersPanel);
            CreateAnalysisButtonsAndProgress(buttonsPanel);
            CreateAnalysisStatusLabel(statusPanel);

            _fileAnalyzerPanel.Controls.Add(analysisControlsPanel);
        }

        private void CreateAnalysisParameterControls(Panel panel)
        {
            var flowLayout = new FlowLayoutPanel();
            flowLayout.Dock = DockStyle.Fill;
            flowLayout.FlowDirection = FlowDirection.LeftToRight;
            flowLayout.WrapContents = false;
            flowLayout.AutoSize = false;
            
            panel.Controls.Add(flowLayout);

            var silenceLabel = new Label();
            silenceLabel.Text = "Silence Threshold:";
            silenceLabel.AutoSize = true;
            silenceLabel.TextAlign = ContentAlignment.MiddleLeft;
            silenceLabel.Margin = new Padding(0, 8, 8, 0);
            flowLayout.Controls.Add(silenceLabel);

            _silenceThresholdNumericUpDown = new NumericUpDown();
            _silenceThresholdNumericUpDown.Minimum = 100;
            _silenceThresholdNumericUpDown.Maximum = 5000;
            _silenceThresholdNumericUpDown.Value = 500;
            _silenceThresholdNumericUpDown.Increment = 100;
            _silenceThresholdNumericUpDown.Width = 80;
            _silenceThresholdNumericUpDown.Margin = new Padding(0, 6, 16, 0);
            flowLayout.Controls.Add(_silenceThresholdNumericUpDown);

            var activityLabel = new Label();
            activityLabel.Text = "Min Activity (ms):";
            activityLabel.AutoSize = true;
            activityLabel.TextAlign = ContentAlignment.MiddleLeft;
            activityLabel.Margin = new Padding(0, 8, 8, 0);
            flowLayout.Controls.Add(activityLabel);

            _minimumActivityNumericUpDown = new NumericUpDown();
            _minimumActivityNumericUpDown.Minimum = 50;
            _minimumActivityNumericUpDown.Maximum = 2000;
            _minimumActivityNumericUpDown.Value = 100;
            _minimumActivityNumericUpDown.Increment = 50;
            _minimumActivityNumericUpDown.Width = 80;
            _minimumActivityNumericUpDown.Margin = new Padding(0, 6, 0, 0);
            flowLayout.Controls.Add(_minimumActivityNumericUpDown);
        }

        private void CreateAnalysisButtonsAndProgress(Panel panel)
        {
            var flowLayout = new FlowLayoutPanel();
            flowLayout.Dock = DockStyle.Fill;
            flowLayout.FlowDirection = FlowDirection.LeftToRight;
            flowLayout.WrapContents = false;
            flowLayout.AutoSize = false;
            
            panel.Controls.Add(flowLayout);

            _analyzeActivityButton = new Button();
            _analyzeActivityButton.Text = "Analyze Activity";
            _analyzeActivityButton.Size = new Size(120, 32);
            _analyzeActivityButton.Margin = new Padding(0, 0, 8, 0);
            flowLayout.Controls.Add(_analyzeActivityButton);

            _analyzeFrequenciesButton = new Button();
            _analyzeFrequenciesButton.Text = "Analyze Frequencies";
            _analyzeFrequenciesButton.Size = new Size(130, 32);
            _analyzeFrequenciesButton.Margin = new Padding(0, 0, 16, 0);
            flowLayout.Controls.Add(_analyzeFrequenciesButton);

            _analysisProgressBar = new ProgressBar();
            _analysisProgressBar.Size = new Size(150, 28);
            _analysisProgressBar.Visible = false;
            _analysisProgressBar.Margin = new Padding(0, 2, 0, 0);
            flowLayout.Controls.Add(_analysisProgressBar);
        }

        private void CreateAnalysisStatusLabel(Panel panel)
        {
            _analysisStatusLabel = new Label();
            _analysisStatusLabel.Text = "Select a file to analyze";
            _analysisStatusLabel.Dock = DockStyle.Fill;
            _analysisStatusLabel.ForeColor = SystemColors.ControlDarkDark;
            _analysisStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _analysisStatusLabel.Padding = new Padding(0, 8, 0, 0);
            panel.Controls.Add(_analysisStatusLabel);
        }

        private void CreateResultsTextBox()
        {
            // Create a container panel for results that will hold both TreeView and RichTextBox
            _resultsContainerPanel = new Panel();
            _resultsContainerPanel.Dock = DockStyle.Fill;
            _fileAnalyzerPanel.Controls.Add(_resultsContainerPanel);

            // Create TreeView for frequency analysis results
            _frequencyTreeView = new TreeView();
            _frequencyTreeView.Dock = DockStyle.Left;
            _frequencyTreeView.Width = 350;
            _frequencyTreeView.CheckBoxes = true;
            _frequencyTreeView.ShowLines = true;
            _frequencyTreeView.ShowPlusMinus = true;
            _frequencyTreeView.ShowRootLines = true;
            _frequencyTreeView.FullRowSelect = true;
            _frequencyTreeView.HideSelection = false;
            _frequencyTreeView.Font = new Font("Segoe UI", 9F);
            _frequencyTreeView.BackColor = SystemColors.Window;
            _frequencyTreeView.BorderStyle = BorderStyle.FixedSingle;
            _frequencyTreeView.Visible = false; // Initially hidden until frequency analysis is run

            // Create splitter
            _resultsSplitter = new Splitter();
            _resultsSplitter.Dock = DockStyle.Left;
            _resultsSplitter.Width = 4;
            _resultsSplitter.BackColor = SystemColors.ControlDark;
            _resultsSplitter.Visible = false; // Initially hidden

            // Create RichTextBox for text results
            _analysisResultsTextBox = new RichTextBox();
            _analysisResultsTextBox.Dock = DockStyle.Fill;
            _analysisResultsTextBox.ReadOnly = true;
            _analysisResultsTextBox.Font = new Font("Consolas", 9F);
            _analysisResultsTextBox.Text = GetInitialHelpText();
            _analysisResultsTextBox.BorderStyle = BorderStyle.FixedSingle;

            // Add controls to results container (order matters for docking)
            _resultsContainerPanel.Controls.Add(_analysisResultsTextBox); // Fill (last)
            _resultsContainerPanel.Controls.Add(_resultsSplitter);          // Left (middle)
            _resultsContainerPanel.Controls.Add(_frequencyTreeView);       // Left (first)

            // Setup TreeView event handlers
            SetupTreeViewEventHandlers();
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

        private void SetupEventHandlers()
        {
            _analyzeActivityButton.Click += OnAnalyzeActivity;
            _analyzeFrequenciesButton.Click += OnAnalyzeFrequencies;
        }

        private void SetupTreeViewEventHandlers()
        {
            _frequencyTreeView.AfterCheck += OnTreeViewAfterCheck;
            _frequencyTreeView.NodeMouseDoubleClick += OnTreeViewNodeDoubleClick;
            _frequencyTreeView.AfterSelect += OnTreeViewAfterSelect;
        }

        #region Event Handlers

        private async void OnAnalyzeActivity(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                _uiService?.ShowWarning("No recording file selected. Please use the Player tab to select a file first.");
                return;
            }

            try
            {
                SetAnalysisInProgress(true);
                _analysisStatusLabel.Text = "Analyzing activity...";
                _analysisStatusLabel.ForeColor = Color.Blue;

                AppendToAnalysisResults($"\n[{DateTime.Now:HH:mm:ss}] Starting audio activity analysis...");
                AppendToAnalysisResults($"File: {Path.GetFileName(_currentFilePath)}");
                AppendToAnalysisResults($"Silence Threshold: {_silenceThresholdNumericUpDown.Value}");
                AppendToAnalysisResults($"Minimum Activity Duration: {_minimumActivityNumericUpDown.Value}ms");

                var silenceThreshold = (int)_silenceThresholdNumericUpDown.Value;
                var minimumActivity = TimeSpan.FromMilliseconds((double)_minimumActivityNumericUpDown.Value);

                var analysis = await Task.Run(() =>
                    FileAnalyzer.AnalyzeAudioActivity(
                        _currentFilePath,
                        silenceThreshold,
                        minimumActivity));

                AppendToAnalysisResults($"\n{analysis}");
                _analysisStatusLabel.Text = "Activity analysis completed";
                _analysisStatusLabel.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error analyzing audio activity");
                AppendToAnalysisResults($"\n[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                _analysisStatusLabel.Text = "Analysis failed";
                _analysisStatusLabel.ForeColor = Color.Red;
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
                _uiService?.ShowWarning("No recording file selected. Please use the Player tab to select a file first.");
                return;
            }

            try
            {
                SetAnalysisInProgress(true);
                _analysisStatusLabel.Text = "Analyzing frequencies...";
                _analysisStatusLabel.ForeColor = Color.Blue;

                AppendToAnalysisResults($"\n[{DateTime.Now:HH:mm:ss}] Starting frequency analysis...");
                AppendToAnalysisResults($"File: {Path.GetFileName(_currentFilePath)}");

                var frequencies = await Task.Run(() =>
                    FileAnalyzer.GetAllFrequencyModulations(_currentFilePath));

                DisplayFrequencyAnalysisResults(frequencies);

                _analysisStatusLabel.Text = "Frequency analysis completed";
                _analysisStatusLabel.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error analyzing frequencies");
                AppendToAnalysisResults($"\n[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                _analysisStatusLabel.Text = "Analysis failed";
                _analysisStatusLabel.ForeColor = Color.Red;
            }
            finally
            {
                SetAnalysisInProgress(false);
            }
        }

        #endregion

        #region Helper Methods

        private void SetAnalysisInProgress(bool inProgress)
        {
            _analyzeActivityButton.Enabled = !inProgress;
            _analyzeFrequenciesButton.Enabled = !inProgress;
            _analysisProgressBar.Visible = inProgress;
            _analysisProgressBar.Style = inProgress ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        }

        private void DisplayFrequencyAnalysisResults(List<ShalevOhad.DCS.SRS.Recorder.Core.Models.FrequencyModulationInfo> frequencies)
        {
            _currentFrequencies = frequencies;

            // Show TreeView and splitter
            _frequencyTreeView.Visible = true;
            _resultsSplitter.Visible = true;

            // Clear and populate TreeView
            _frequencyTreeView.BeginUpdate();
            _frequencyTreeView.Nodes.Clear();

            AppendToAnalysisResults($"\n=== Frequency Analysis Results ===");
            AppendToAnalysisResults($"Total unique frequency-modulation combinations: {frequencies.Count}");
            AppendToAnalysisResults($"");

            // Group frequencies by coalition
            var coalitionGroups = frequencies
                .SelectMany(f => f.Players.Select(p => new { Frequency = f, Player = p }))
                .GroupBy(x => x.Player.Coalition ?? "Unknown")
                .OrderBy(g => g.Key);

            foreach (var coalitionGroup in coalitionGroups)
            {
                var coalitionName = coalitionGroup.Key;
                var coalitionNode = new TreeNode($"{coalitionName} ({coalitionGroup.GroupBy(x => x.Frequency).Count()} frequencies)")
                {
                    Tag = new { Type = "Coalition", Name = coalitionName },
                    ImageIndex = -1,
                    SelectedImageIndex = -1,
                    ForeColor = GetCoalitionColor(coalitionName),
                    NodeFont = new Font(_frequencyTreeView.Font, FontStyle.Bold)
                };

                // Group by frequency within coalition
                var frequencyGroups = coalitionGroup
                    .GroupBy(x => x.Frequency)
                    .OrderBy(fg => fg.Key.Frequency);

                foreach (var freqGroup in frequencyGroups)
                {
                    var freq = freqGroup.Key;
                    var players = freqGroup.Select(x => x.Player).Distinct().ToList();
                    
                    var freqNode = new TreeNode($"{freq.Frequency:F1} MHz ({freq.Modulation}) - {players.Count} users")
                    {
                        Tag = new { Type = "Frequency", Frequency = freq, Players = players },
                        Checked = true, // Default to checked
                        ForeColor = SystemColors.WindowText
                    };

                    // Add players as child nodes
                    foreach (var player in players.OrderBy(p => p.Name))
                    {
                        var duration = player.LastSeen - player.FirstSeen;
                        var playerText = $"{player.Name} ({player.Aircraft}) - {player.PacketCount:N0} packets, {FormatDuration(duration)}";
                        
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

            AppendToAnalysisResults($"Use the tree view on the left to explore frequencies by coalition and select specific frequencies for detailed analysis.");
            AppendToAnalysisResults($"Double-click on any item for detailed information.");
        }

        private void AppendToAnalysisResults(string message)
        {
            _uiService?.InvokeOnUIThread(() =>
            {
                _analysisResultsTextBox.AppendText(message + Environment.NewLine);
                _analysisResultsTextBox.ScrollToCaret();
            });
        }

        #endregion

        #region TreeView Event Handlers

        private void OnTreeViewAfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag == null) return;

            try
            {
                var nodeData = e.Node.Tag;
                var nodeType = nodeData.GetType().GetProperty("Type")?.GetValue(nodeData)?.ToString();

                if (nodeType == "Frequency")
                {
                    // When a frequency is checked/unchecked, update the analysis display
                    UpdateFrequencySelection();
                }
                else if (nodeType == "Coalition")
                {
                    // When a coalition is checked/unchecked, check/uncheck all its frequencies
                    foreach (TreeNode freqNode in e.Node.Nodes)
                    {
                        freqNode.Checked = e.Node.Checked;
                    }
                    UpdateFrequencySelection();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling tree view check change");
            }
        }

        private void OnTreeViewNodeDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag == null) return;

            try
            {
                var nodeData = e.Node.Tag;
                var nodeType = nodeData.GetType().GetProperty("Type")?.GetValue(nodeData)?.ToString();

                switch (nodeType)
                {
                    case "Coalition":
                        var coalitionName = nodeData.GetType().GetProperty("Name")?.GetValue(nodeData)?.ToString();
                        ShowCoalitionDetails(coalitionName);
                        break;
                    
                    case "Frequency":
                        var frequency = nodeData.GetType().GetProperty("Frequency")?.GetValue(nodeData);
                        var players = nodeData.GetType().GetProperty("Players")?.GetValue(nodeData);
                        ShowFrequencyDetails(frequency, players);
                        break;
                    
                    case "Player":
                        var player = nodeData.GetType().GetProperty("Player")?.GetValue(nodeData);
                        var freq = nodeData.GetType().GetProperty("Frequency")?.GetValue(nodeData);
                        ShowPlayerDetails(player, freq);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling tree view double click");
            }
        }

        private void OnTreeViewAfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag == null) return;

            try
            {
                var nodeData = e.Node.Tag;
                var nodeType = nodeData.GetType().GetProperty("Type")?.GetValue(nodeData)?.ToString();

                // Show quick info in status label
                switch (nodeType)
                {
                    case "Coalition":
                        var coalitionName = nodeData.GetType().GetProperty("Name")?.GetValue(nodeData)?.ToString();
                        _analysisStatusLabel.Text = $"Selected coalition: {coalitionName}";
                        break;
                    
                    case "Frequency":
                        var frequency = nodeData.GetType().GetProperty("Frequency")?.GetValue(nodeData);
                        if (frequency != null)
                        {
                            var freqValue = frequency.GetType().GetProperty("Frequency")?.GetValue(frequency);
                            var modulation = frequency.GetType().GetProperty("Modulation")?.GetValue(frequency);
                            _analysisStatusLabel.Text = $"Selected frequency: {freqValue:F1} MHz ({modulation})";
                        }
                        break;
                    
                    case "Player":
                        var player = nodeData.GetType().GetProperty("Player")?.GetValue(nodeData);
                        if (player != null)
                        {
                            var playerName = player.GetType().GetProperty("Name")?.GetValue(player);
                            var aircraft = player.GetType().GetProperty("Aircraft")?.GetValue(player);
                            _analysisStatusLabel.Text = $"Selected player: {playerName} ({aircraft})";
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling tree view selection");
            }
        }

        #endregion

        #region TreeView Helper Methods

        private void UpdateFrequencySelection()
        {
            try
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

                AppendToAnalysisResults($"\n[{DateTime.Now:HH:mm:ss}] Selection updated: {selectedFrequencies.Count} frequencies selected");
                
                if (selectedFrequencies.Count > 0)
                {
                    _analysisStatusLabel.Text = $"{selectedFrequencies.Count} frequencies selected";
                    _analysisStatusLabel.ForeColor = Color.Green;
                }
                else
                {
                    _analysisStatusLabel.Text = "No frequencies selected";
                    _analysisStatusLabel.ForeColor = Color.Orange;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error updating frequency selection");
            }
        }

        private void ShowCoalitionDetails(string? coalitionName)
        {
            if (string.IsNullOrEmpty(coalitionName)) return;

            AppendToAnalysisResults($"\n=== Coalition Details: {coalitionName} ===");
            
            var coalitionFreqs = _currentFrequencies
                .Where(f => f.Players.Any(p => p.Coalition == coalitionName))
                .ToList();

            AppendToAnalysisResults($"Frequencies used: {coalitionFreqs.Count}");
            
            var totalPlayers = coalitionFreqs.SelectMany(f => f.Players)
                .Where(p => p.Coalition == coalitionName)
                .DistinctBy(p => p.Name)
                .Count();
            
            AppendToAnalysisResults($"Total players: {totalPlayers}");

            foreach (var freq in coalitionFreqs.OrderBy(f => f.Frequency))
            {
                var coalitionPlayers = freq.Players.Where(p => p.Coalition == coalitionName).ToList();
                AppendToAnalysisResults($"  • {freq.Frequency:F1} MHz ({freq.Modulation}) - {coalitionPlayers.Count} players");
            }
        }

        private void ShowFrequencyDetails(dynamic? frequency, dynamic? players)
        {
            if (frequency == null || players == null) return;

            var freqValue = frequency.GetType().GetProperty("Frequency")?.GetValue(frequency);
            var modulation = frequency.GetType().GetProperty("Modulation")?.GetValue(frequency);
            
            AppendToAnalysisResults($"\n=== Frequency Details: {freqValue:F1} MHz ({modulation}) ===");
            
            var playersList = players as System.Collections.IEnumerable;
            if (playersList != null)
            {
                var playerCount = 0;
                foreach (var player in playersList)
                {
                    playerCount++;
                    var name = player.GetType().GetProperty("Name")?.GetValue(player);
                    var aircraft = player.GetType().GetProperty("Aircraft")?.GetValue(player);
                    var coalition = player.GetType().GetProperty("Coalition")?.GetValue(player);
                    var packetCount = player.GetType().GetProperty("PacketCount")?.GetValue(player);
                    var firstSeen = player.GetType().GetProperty("FirstSeen")?.GetValue(player);
                    var lastSeen = player.GetType().GetProperty("LastSeen")?.GetValue(player);
                    
                    if (firstSeen is DateTime first && lastSeen is DateTime last)
                    {
                        var duration = last - first;
                        AppendToAnalysisResults($"  • {name} ({coalition}) - {aircraft}");
                        AppendToAnalysisResults($"    Packets: {packetCount:N0}, Duration: {FormatDuration(duration)}");
                        AppendToAnalysisResults($"    Active: {first:HH:mm:ss} - {last:HH:mm:ss}");
                    }
                }
                
                AppendToAnalysisResults($"Total players on this frequency: {playerCount}");
            }
        }

        private void ShowPlayerDetails(dynamic? player, dynamic? frequency)
        {
            if (player == null || frequency == null) return;

            var name = player.GetType().GetProperty("Name")?.GetValue(player);
            var aircraft = player.GetType().GetProperty("Aircraft")?.GetValue(player);
            var coalition = player.GetType().GetProperty("Coalition")?.GetValue(player);
            var packetCount = player.GetType().GetProperty("PacketCount")?.GetValue(player);
            var firstSeen = player.GetType().GetProperty("FirstSeen")?.GetValue(player);
            var lastSeen = player.GetType().GetProperty("LastSeen")?.GetValue(player);
            
            var freqValue = frequency.GetType().GetProperty("Frequency")?.GetValue(frequency);
            var modulation = frequency.GetType().GetProperty("Modulation")?.GetValue(frequency);
            
            AppendToAnalysisResults($"\n=== Player Details: {name} ===");
            AppendToAnalysisResults($"Coalition: {coalition}");
            AppendToAnalysisResults($"Aircraft: {aircraft}");
            AppendToAnalysisResults($"Frequency: {freqValue:F1} MHz ({modulation})");
            AppendToAnalysisResults($"Total packets: {packetCount:N0}");
            
            if (firstSeen is DateTime first && lastSeen is DateTime last)
            {
                var duration = last - first;
                AppendToAnalysisResults($"Active period: {first:HH:mm:ss} - {last:HH:mm:ss}");
                AppendToAnalysisResults($"Duration: {FormatDuration(duration)}");
                
                if (duration.TotalSeconds > 0 && packetCount is int packets)
                {
                    var packetsPerSecond = packets / duration.TotalSeconds;
                    AppendToAnalysisResults($"Average rate: {packetsPerSecond:F1} packets/second");
                }
            }
        }

        private static Color GetCoalitionColor(string coalition)
        {
            return coalition.ToLowerInvariant() switch
            {
                "red" => Color.DarkRed,
                "blue" => Color.DarkBlue,
                "neutral" => Color.DarkGreen,
                _ => Color.Black
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

        #endregion

        /// <summary>
        /// Sets the current file for analysis (called from shared file coordination)
        /// </summary>
        public void SetSharedFilePath(string filePath)
        {
            if (_currentFilePath == filePath) return; // Already has this file

            Logger.Debug($"File analyzer tab receiving shared file path: {filePath}");
            SetCurrentFile(filePath);
        }

        /// <summary>
        /// Sets the current file path and updates the UI
        /// </summary>
        private void SetCurrentFile(string filePath)
        {
            _currentFilePath = filePath;
            
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                _currentFilePathLabel.Text = filePath;
                _currentFilePathLabel.ForeColor = SystemColors.WindowText;
                _analysisStatusLabel.Text = "File ready for analysis";
                _analysisStatusLabel.ForeColor = Color.Green;
                
                // Enable analysis buttons
                _analyzeActivityButton.Enabled = true;
                _analyzeFrequenciesButton.Enabled = true;
                
                AppendToAnalysisResults($"\n[{DateTime.Now:HH:mm:ss}] File selected: {Path.GetFileName(filePath)}");
                AppendToAnalysisResults($"Full path: {filePath}");
            }
            else
            {
                _currentFilePathLabel.Text = "No file selected. Please use the Player tab to select a recording file.";
                _currentFilePathLabel.ForeColor = SystemColors.GrayText;
                _analysisStatusLabel.Text = "No file selected";
                _analysisStatusLabel.ForeColor = SystemColors.ControlDarkDark;
                
                // Disable analysis buttons until file is selected
                _analyzeActivityButton.Enabled = false;
                _analyzeFrequenciesButton.Enabled = false;
            }
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            Name = "FileAnalyzerTabView";
            Size = new Size(800, 500);

            ResumeLayout(false);
        }
    }
}