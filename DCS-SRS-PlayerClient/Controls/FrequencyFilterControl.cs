using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Services;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Controls
{
    /// <summary>
    /// Enhanced frequency filtering control with improved UX
    /// </summary>
    public partial class FrequencyFilterControl : UserControl
    {
        #region Fields

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private CheckBox? _enableFilterCheckBox;
        private TreeView? _frequencyTreeView;
        private Button? _selectAllButton;
        private Button? _selectNoneButton;
        private Button? _expandCollapseButton;
        private Label? _statusLabel;
        private ToolTip? _tooltip;

        private List<FrequencyModulationInfo> _availableFrequencies = new();
        private bool _suppressEvents;
        // Add flag to prevent programmatic changes from firing events
        private bool _suppressSelectedFrequenciesChangedEvent;

        #endregion

        #region Events

        /// <summary>
        /// Raised when the filter enabled state changes
        /// </summary>
        public event EventHandler<bool>? FilterEnabledChanged;

        /// <summary>
        /// Raised when the selected frequencies change
        /// </summary>
        public event EventHandler<List<FrequencyModulationInfo>>? SelectedFrequenciesChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether frequency filtering is enabled
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsFilterEnabled
        {
            get => _enableFilterCheckBox?.Checked ?? false;
            set
            {
                if (_enableFilterCheckBox != null && _enableFilterCheckBox.Checked != value)
                {
                    _enableFilterCheckBox.Checked = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the available frequencies for filtering
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<FrequencyModulationInfo> AvailableFrequencies
        {
            get => _availableFrequencies;
            set
            {
                _availableFrequencies = value ?? new List<FrequencyModulationInfo>();
                PopulateFrequencyTree();
                UpdateControlStates();
                UpdateStatusLabel();
            }
        }

        /// <summary>
        /// Gets the currently selected frequencies
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<FrequencyModulationInfo> SelectedFrequencies => GetSelectedFrequencies();

        /// <summary>
        /// Gets whether any frequencies are available
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HasFrequencies => _availableFrequencies.Any();

        #endregion

        #region Constructor

        public FrequencyFilterControl()
        {
            InitializeComponent();
            InitializeControls();
            SetDefaultValues();
        }

        #endregion

        #region Initialization

        private void InitializeControls()
        {
            SuspendLayout();

            try
            {
                // Use proper docking layout to avoid overlap issues
                
                // Status label (at bottom)
                _statusLabel = new Label();
                _statusLabel.Text = "No frequencies available";
                _statusLabel.Height = 28;
                _statusLabel.Dock = DockStyle.Bottom;
                _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
                _statusLabel.ForeColor = SystemColors.GrayText;
                _statusLabel.Padding = new Padding(4, 4, 4, 4);
                
                // Set the font safely
                try
                {
                    _statusLabel.Font = new Font(Font, FontStyle.Italic);
                }
                catch
                {
                    // Fallback to default font if there's an issue
                    _statusLabel.Font = new Font("Segoe UI", 8.25F, FontStyle.Italic);
                }

                // Button panel (at top, below checkbox)
                var buttonPanel = new TableLayoutPanel();
                buttonPanel.Height = 38;
                buttonPanel.Dock = DockStyle.Top;
                buttonPanel.ColumnCount = 3;
                buttonPanel.RowCount = 1;
                buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
                buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
                buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
                buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                buttonPanel.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
                buttonPanel.Margin = new Padding(0);
                buttonPanel.Padding = new Padding(4, 4, 4, 4);

                _selectAllButton = CreateModernButton("Select All");
                _selectAllButton.Dock = DockStyle.Fill;
                _selectAllButton.Margin = new Padding(0, 0, 2, 0);
                _selectAllButton.Enabled = false;
                _selectAllButton.Click += OnSelectAllClick;

                _selectNoneButton = CreateModernButton("Select None");
                _selectNoneButton.Dock = DockStyle.Fill;
                _selectNoneButton.Margin = new Padding(1, 0, 1, 0);
                _selectNoneButton.Enabled = false;
                _selectNoneButton.Click += OnSelectNoneClick;

                _expandCollapseButton = CreateModernButton("Collapse All");
                _expandCollapseButton.Dock = DockStyle.Fill;
                _expandCollapseButton.Margin = new Padding(2, 0, 0, 0);
                _expandCollapseButton.Enabled = false;
                _expandCollapseButton.Click += OnExpandCollapseClick;

                buttonPanel.Controls.Add(_selectAllButton, 0, 0);
                buttonPanel.Controls.Add(_selectNoneButton, 1, 0);
                buttonPanel.Controls.Add(_expandCollapseButton, 2, 0);

                // Enable filter checkbox (at top)
                _enableFilterCheckBox = new CheckBox();
                _enableFilterCheckBox.Text = "Enable Frequency Filtering";
                _enableFilterCheckBox.Height = 32;
                _enableFilterCheckBox.Dock = DockStyle.Top;
                _enableFilterCheckBox.UseVisualStyleBackColor = true;
                _enableFilterCheckBox.Enabled = false;
                _enableFilterCheckBox.Padding = new Padding(4, 8, 4, 4);
                _enableFilterCheckBox.CheckedChanged += OnEnableFilterCheckedChanged;

                // Frequency tree view (fills remaining space)
                _frequencyTreeView = new TreeView();
                _frequencyTreeView.Dock = DockStyle.Fill;
                _frequencyTreeView.CheckBoxes = true;
                _frequencyTreeView.ShowLines = true;
                _frequencyTreeView.ShowPlusMinus = true;
                _frequencyTreeView.ShowRootLines = true;
                _frequencyTreeView.FullRowSelect = false;
                _frequencyTreeView.HideSelection = false;
                _frequencyTreeView.Enabled = false;
                _frequencyTreeView.Margin = new Padding(4, 0, 4, 0);
                _frequencyTreeView.BeforeCheck += OnTreeBeforeCheck;
                _frequencyTreeView.AfterCheck += OnTreeAfterCheck;
                _frequencyTreeView.NodeMouseHover += OnTreeNodeMouseHover;

                // Tooltip
                _tooltip = new ToolTip();
                _tooltip.AutoPopDelay = 5000;
                _tooltip.InitialDelay = 500;
                _tooltip.ReshowDelay = 500;
                _tooltip.ShowAlways = true;

                // Set tooltips for buttons
                _tooltip.SetToolTip(_selectAllButton, "Select all available frequencies for filtering");
                _tooltip.SetToolTip(_selectNoneButton, "Deselect all frequencies (disable filtering)");
                _tooltip.SetToolTip(_expandCollapseButton, "Expand or collapse all frequency groups");

                // Add controls in proper docking order
                // Bottom-docked controls should be added first
                Controls.Add(_statusLabel);
                // Then top-docked controls
                Controls.Add(_enableFilterCheckBox);
                Controls.Add(buttonPanel);
                // Fill-docked controls should be added last
                Controls.Add(_frequencyTreeView);

                // Remove any padding/margin that could cause positioning issues
                Padding = new Padding(0);
                Margin = new Padding(0);

                // Set default size
                Size = new Size(350, 400);
                MinimumSize = new Size(250, 200);
            }
            finally
            {
                ResumeLayout(true);
                PerformLayout();
            }
        }

        private void SetDefaultValues()
        {
            UpdateControlStates();
            UpdateStatusLabel();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Selects all available frequencies
        /// </summary>
        public void SelectAllFrequencies()
        {
            if (_frequencyTreeView == null) return;

            _frequencyTreeView.AfterCheck -= OnTreeAfterCheck;
            _suppressEvents = true;
            _suppressSelectedFrequenciesChangedEvent = true;
            
            try
            {
                foreach (TreeNode coalitionNode in _frequencyTreeView.Nodes)
                {
                    foreach (TreeNode frequencyNode in coalitionNode.Nodes)
                    {
                        frequencyNode.Checked = true;
                    }
                }
                UpdateStatusLabel();
            }
            finally
            {
                _suppressEvents = false;
                _suppressSelectedFrequenciesChangedEvent = false;
                _frequencyTreeView.AfterCheck += OnTreeAfterCheck;
                OnSelectedFrequenciesChanged();
            }
        }

        /// <summary>
        /// Clears all frequency selections
        /// </summary>
        public void ClearAllSelections()
        {
            if (_frequencyTreeView == null) return;

            _frequencyTreeView.AfterCheck -= OnTreeAfterCheck;
            _suppressEvents = true;
            _suppressSelectedFrequenciesChangedEvent = true;
            
            try
            {
                foreach (TreeNode coalitionNode in _frequencyTreeView.Nodes)
                {
                    foreach (TreeNode frequencyNode in coalitionNode.Nodes)
                    {
                        frequencyNode.Checked = false;
                    }
                }
                UpdateStatusLabel();
            }
            finally
            {
                _suppressEvents = false;
                _suppressSelectedFrequenciesChangedEvent = false;
                _frequencyTreeView.AfterCheck += OnTreeAfterCheck;
                OnSelectedFrequenciesChanged();
            }
        }

        /// <summary>
        /// Gets the current frequency filter configuration
        /// </summary>
        public Models.FrequencyFilterConfig GetCurrentFilter()
        {
            return new Models.FrequencyFilterConfig(IsFilterEnabled, GetSelectedFrequencies());
        }

        /// <summary>
        /// Sets the available frequencies from recording information
        /// </summary>
        public void SetAvailableFrequencies(List<FrequencyModulationInfo> frequencies)
        {
            AvailableFrequencies = frequencies;
        }

        /// <summary>
        /// Event raised when the frequency filter changes (for compatibility)
        /// </summary>
        public event EventHandler<Models.FrequencyFilterConfig>? FilterChanged;

        /// <summary>
        /// Sets selected frequencies programmatically
        /// </summary>
        public void SetSelectedFrequencies(IEnumerable<FrequencyModulationInfo> frequencies)
        {
            if (_frequencyTreeView == null) return;

            var selectedSet = new HashSet<(double Frequency, Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player.Modulation Modulation)>(
                frequencies.Select(f => (f.Frequency, f.Modulation)));

            // Get current selection to compare and avoid unnecessary updates
            var currentSelection = new HashSet<(double Frequency, Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player.Modulation Modulation)>(
                GetSelectedFrequencies().Select(f => (f.Frequency, f.Modulation)));

            // If selections are identical, no need to update
            if (selectedSet.SetEquals(currentSelection))
            {
                Logger.Debug("SetSelectedFrequencies called with identical selection, skipping update");
                return;
            }

            // Temporarily unsubscribe from the event to prevent recursive calls
            _frequencyTreeView.AfterCheck -= OnTreeAfterCheck;
            _suppressEvents = true;
            _suppressSelectedFrequenciesChangedEvent = true; // Prevent external event firing
            
            try
            {
                foreach (TreeNode coalitionNode in _frequencyTreeView.Nodes)
                {
                    foreach (TreeNode frequencyNode in coalitionNode.Nodes)
                    {
                        if (frequencyNode.Tag is FrequencyModulationInfo freqMod)
                        {
                            bool shouldBeChecked = selectedSet.Contains((freqMod.Frequency, freqMod.Modulation));
                            if (frequencyNode.Checked != shouldBeChecked)
                            {
                                frequencyNode.Checked = shouldBeChecked;
                            }
                        }
                    }
                }
                
                // Update the status label after programmatic changes
                UpdateStatusLabel();
            }
            finally
            {
                _suppressEvents = false;
                _suppressSelectedFrequenciesChangedEvent = false; // Re-enable event firing
                // Re-subscribe to the event
                _frequencyTreeView.AfterCheck += OnTreeAfterCheck;
                // Don't call OnSelectedFrequenciesChanged() here - let the UI update naturally
            }
        }

        #endregion

        #region Private Methods

        private void PopulateFrequencyTree()
        {
            if (_frequencyTreeView == null) return;

            _frequencyTreeView.BeginUpdate();
            _frequencyTreeView.Nodes.Clear();

            if (!_availableFrequencies.Any())
            {
                _frequencyTreeView.EndUpdate();
                return;
            }

            try
            {
                // Group frequencies by coalition
                var coalitionGroups = GroupFrequenciesByCoalition();

                // Create coalition nodes
                var coalitionOrder = new[] { "Blue", "Red", "Spectator", "Unknown" };

                foreach (var coalitionName in coalitionOrder)
                {
                    if (coalitionGroups.TryGetValue(coalitionName, out var frequencies) && frequencies.Any())
                    {
                        CreateCoalitionNode(coalitionName, frequencies);
                    }
                }

                // Add any remaining coalitions not in the standard order
                foreach (var kvp in coalitionGroups.Where(g => !coalitionOrder.Contains(g.Key)))
                {
                    CreateCoalitionNode(kvp.Key, kvp.Value);
                }

                _frequencyTreeView.ExpandAll();
                
                // Update the expand/collapse button text since we just expanded all
                if (_expandCollapseButton != null)
                {
                    _expandCollapseButton.Text = "Collapse All";
                    _tooltip?.SetToolTip(_expandCollapseButton, "Collapse all frequency groups");
                }
            }
            finally
            {
                _frequencyTreeView.EndUpdate();
            }
        }

        private Dictionary<string, List<FrequencyModulationInfo>> GroupFrequenciesByCoalition()
        {
            var coalitionGroups = new Dictionary<string, List<FrequencyModulationInfo>>();

            foreach (var freqMod in _availableFrequencies)
            {
                var coalitionsOnFreq = freqMod.Players
                    .Select(p => p.Coalition)
                    .Distinct()
                    .Where(c => !string.IsNullOrEmpty(c))
                    .DefaultIfEmpty("Unknown")
                    .ToList();

                foreach (var coalition in coalitionsOnFreq)
                {
                    if (!coalitionGroups.ContainsKey(coalition))
                    {
                        coalitionGroups[coalition] = new List<FrequencyModulationInfo>();
                    }

                    var coalitionPlayers = freqMod.Players
                        .Where(p => p.Coalition == coalition || (string.IsNullOrEmpty(p.Coalition) && coalition == "Unknown"))
                        .ToList();

                    if (coalitionPlayers.Any())
                    {
                        var coalitionFreqMod = freqMod with { Players = coalitionPlayers };
                        coalitionGroups[coalition].Add(coalitionFreqMod);
                    }
                }
            }

            return coalitionGroups;
        }

        private void CreateCoalitionNode(string coalitionName, List<FrequencyModulationInfo> frequencies)
        {
            if (_frequencyTreeView == null) return;

            var totalPlayers = frequencies.SelectMany(f => f.Players).Select(p => p.TransmitterGuid).Distinct().Count();
            var coalitionNode = new TreeNode($"{coalitionName} Coalition ({frequencies.Count} freq, {totalPlayers} players)");
            coalitionNode.ForeColor = GetCoalitionColor(coalitionName);
            
            // Set the font safely
            try
            {
                coalitionNode.NodeFont = new Font(_frequencyTreeView.Font, FontStyle.Bold);
            }
            catch
            {
                // Fallback to default font if there's an issue
                coalitionNode.NodeFont = new Font("Segoe UI", 8.25F, FontStyle.Bold);
            }

            foreach (var freqMod in frequencies.OrderBy(f => f.Frequency).ThenBy(f => f.Modulation))
            {
                var frequencyNode = new TreeNode(freqMod.GetDisplayText())
                {
                    Tag = freqMod,
                    ForeColor = Color.Black
                };

                foreach (var player in freqMod.Players.OrderBy(p => p.Name))
                {
                    var playerNode = new TreeNode(player.GetDisplayText())
                    {
                        Tag = player,
                        ForeColor = Color.Gray
                    };

                    frequencyNode.Nodes.Add(playerNode);
                }

                coalitionNode.Nodes.Add(frequencyNode);
            }

            _frequencyTreeView.Nodes.Add(coalitionNode);
        }

        private List<FrequencyModulationInfo> GetSelectedFrequencies()
        {
            var selected = new List<FrequencyModulationInfo>();
            
            if (_frequencyTreeView == null) return selected;

            foreach (TreeNode coalitionNode in _frequencyTreeView.Nodes)
            {
                foreach (TreeNode frequencyNode in coalitionNode.Nodes)
                {
                    if (frequencyNode.Checked && frequencyNode.Tag is FrequencyModulationInfo freqMod)
                    {
                        selected.Add(freqMod);
                    }
                }
            }

            return selected;
        }

        private void UpdateControlStates()
        {
            var hasFrequencies = _availableFrequencies.Any();
            var isFilterEnabled = IsFilterEnabled && hasFrequencies;

            if (_enableFilterCheckBox != null)
                _enableFilterCheckBox.Enabled = hasFrequencies;

            if (_frequencyTreeView != null)
                _frequencyTreeView.Enabled = isFilterEnabled;

            if (_selectAllButton != null)
                _selectAllButton.Enabled = isFilterEnabled;

            if (_selectNoneButton != null)
                _selectNoneButton.Enabled = isFilterEnabled;

            if (_expandCollapseButton != null)
                _expandCollapseButton.Enabled = isFilterEnabled;
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel == null) return;

            if (!HasFrequencies)
            {
                _statusLabel.Text = "No frequencies available";
                _statusLabel.ForeColor = SystemColors.GrayText;
                return;
            }

            var selectedCount = GetSelectedFrequencies().Count;
            var totalCount = _availableFrequencies.Count;

            if (IsFilterEnabled)
            {
                _statusLabel.Text = $"Filter enabled: {selectedCount} of {totalCount} frequencies selected";
                _statusLabel.ForeColor = selectedCount > 0 ? SystemColors.ControlText : Color.OrangeRed;
            }
            else
            {
                _statusLabel.Text = $"Filter disabled: {totalCount} frequencies available";
                _statusLabel.ForeColor = SystemColors.GrayText;
            }
        }

        private static Color GetCoalitionColor(string coalition) => coalition switch
        {
            "Red" => Color.DarkRed,
            "Blue" => Color.DarkBlue,
            "Spectator" => Color.DarkGreen,
            "Unknown" => Color.DarkGray,
            _ => Color.Black
        };

        #endregion

        #region Event Handlers

        private void OnEnableFilterCheckedChanged(object? sender, EventArgs e)
        {
            UpdateControlStates();
            UpdateStatusLabel();

            if (!IsFilterEnabled)
            {
                ClearAllSelections();
            }
            else if (!GetSelectedFrequencies().Any() && HasFrequencies)
            {
                // Auto-select all when enabling filter for the first time
                SelectAllFrequencies();
            }

            FilterEnabledChanged?.Invoke(this, IsFilterEnabled);
        }

        private void OnSelectAllClick(object? sender, EventArgs e)
        {
            SelectAllFrequencies();
            UpdateStatusLabel();
        }

        private void OnSelectNoneClick(object? sender, EventArgs e)
        {
            ClearAllSelections();
            UpdateStatusLabel();
        }

        private void OnExpandCollapseClick(object? sender, EventArgs e)
        {
            if (_frequencyTreeView == null || _expandCollapseButton == null) return;

            var shouldExpand = _expandCollapseButton.Text.Contains("Expand");
            
            if (shouldExpand)
            {
                _frequencyTreeView.ExpandAll();
                _expandCollapseButton.Text = "Collapse All";
                _tooltip?.SetToolTip(_expandCollapseButton, "Collapse all frequency groups");
            }
            else
            {
                _frequencyTreeView.CollapseAll();
                _expandCollapseButton.Text = "Expand All";
                _tooltip?.SetToolTip(_expandCollapseButton, "Expand all frequency groups");
            }
        }

        private void OnTreeBeforeCheck(object? sender, TreeViewCancelEventArgs e)
        {
            // Only allow checking of frequency nodes (middle level)
            if (e.Node?.Parent == null) // Coalition node (top level)
            {
                e.Cancel = true;
            }
            else if (e.Node?.Parent?.Parent != null) // Player node (has grandparent)
            {
                e.Cancel = true;
            }
        }

        private void OnTreeAfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (_suppressEvents) return;

            UpdateStatusLabel();
            OnSelectedFrequenciesChanged();
        }

        private void OnTreeNodeMouseHover(object? sender, TreeNodeMouseHoverEventArgs e)
        {
            if (_tooltip == null || _frequencyTreeView == null) return;

            string tooltipText = "";
            
            if (e.Node?.Tag is PlayerFrequencyInfo player)
            {
                tooltipText = PlayerTooltipBuilder.BuildPlayerFrequencyTooltip(player);
            }
            else if (e.Node?.Tag is FrequencyModulationInfo freqMod)
            {
                tooltipText = $"Frequency: {freqMod.Frequency:F3} MHz\n" +
                             $"Modulation: {freqMod.Modulation}\n" +
                             $"Players: {freqMod.Players.Count}";
            }

            _tooltip.SetToolTip(_frequencyTreeView, tooltipText);
        }

        private void OnSelectedFrequenciesChanged()
        {
            if (_suppressEvents || _suppressSelectedFrequenciesChangedEvent) return;

            var selectedFrequencies = GetSelectedFrequencies();
            SelectedFrequenciesChanged?.Invoke(this, selectedFrequencies);
            
            // Also fire the FilterChanged event for compatibility
            var filterConfig = new Models.FrequencyFilterConfig(IsFilterEnabled, selectedFrequencies);
            FilterChanged?.Invoke(this, filterConfig);
            
            Logger.Debug($"Selected frequencies changed: {selectedFrequencies.Count} selected");
        }

        #endregion

        #region Component Designer

        private void InitializeComponent()
        {
            SuspendLayout();
            
            Name = "FrequencyFilterControl";
            
            ResumeLayout(false);
        }

        #endregion

        #region UI Helper Methods

        private Button CreateModernButton(string text)
        {
            var button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.FromArgb(220, 240, 255);
            button.ForeColor = Color.FromArgb(40, 60, 100);
            button.Font = new Font("Segoe UI", 8.25F, FontStyle.Regular);
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.Height = 28;

            // Modern flat button style
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(150, 180, 220);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 230, 255);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 220, 255);

            // Add hover effects
            button.MouseEnter += (s, e) => {
                if (button.Enabled)
                    button.BackColor = Color.FromArgb(200, 230, 255);
            };

            button.MouseLeave += (s, e) => {
                if (button.Enabled)
                    button.BackColor = Color.FromArgb(220, 240, 255);
            };

            button.EnabledChanged += (s, e) => {
                if (button.Enabled)
                {
                    button.BackColor = Color.FromArgb(220, 240, 255);
                    button.ForeColor = Color.FromArgb(40, 60, 100);
                }
                else
                {
                    button.BackColor = Color.FromArgb(240, 240, 240);
                    button.ForeColor = Color.FromArgb(120, 120, 120);
                }
            };

            return button;
        }

        #endregion

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tooltip?.Dispose();
                _enableFilterCheckBox?.Dispose();
                _frequencyTreeView?.Dispose();
                _selectAllButton?.Dispose();
                _selectNoneButton?.Dispose();
                _expandCollapseButton?.Dispose();
                _statusLabel?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}