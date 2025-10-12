using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Components
{
    /// <summary>
    /// Shared component for displaying frequency modulation information in a tree view
    /// </summary>
    public partial class FrequencyTreeViewComponent : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Controls
        
        private Panel _containerPanel;
        private Panel _headerPanel;
        private Label _headerLabel;
        private TreeView _treeView;
        
        #endregion

        #region State
        
        private List<FrequencyModulationInfo> _frequencies = new();
        private bool _showPlayerDetails;
        private bool _allowSelection;
        
        #endregion

        #region Events
        
        public event EventHandler<TreeViewEventArgs>? NodeChecked;
        public event EventHandler<TreeNodeMouseClickEventArgs>? NodeDoubleClicked;
        public event EventHandler<TreeViewEventArgs>? NodeSelected;
        public event EventHandler? SelectionChanged;
        
        #endregion

        #region Properties
        
        /// <summary>Gets or sets whether to show detailed player information</summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool ShowPlayerDetails
        {
            get => _showPlayerDetails;
            set
            {
                _showPlayerDetails = value;
                RefreshTreeView();
            }
        }

        /// <summary>Gets or sets whether to allow node selection with checkboxes</summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool AllowSelection
        {
            get => _allowSelection;
            set
            {
                _allowSelection = value;
                _treeView.CheckBoxes = value;
                RefreshTreeView();
            }
        }

        /// <summary>Gets or sets the header text</summary>
        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public string HeaderText
        {
            get => _headerLabel.Text;
            set => _headerLabel.Text = value;
        }

        /// <summary>Gets the currently selected frequency modulation combinations</summary>
        public IEnumerable<FrequencyModulationInfo> SelectedFrequencies
        {
            get
            {
                if (!_allowSelection) return Enumerable.Empty<FrequencyModulationInfo>();

                var selected = new List<FrequencyModulationInfo>();
                
                foreach (TreeNode coalitionNode in _treeView.Nodes)
                {
                    foreach (TreeNode freqNode in coalitionNode.Nodes)
                    {
                        if (freqNode.Checked && freqNode.Tag != null)
                        {
                            var nodeData = freqNode.Tag;
                            var frequency = nodeData.GetType().GetProperty("Frequency")?.GetValue(nodeData);
                            if (frequency is FrequencyModulationInfo freqInfo)
                            {
                                selected.Add(freqInfo);
                            }
                        }
                    }
                }
                
                return selected;
            }
        }

        /// <summary>Gets the underlying TreeView control for advanced customization</summary>
        public TreeView TreeView => _treeView;
        
        #endregion

        public FrequencyTreeViewComponent()
        {
            InitializeComponent();
            CreateControls();
            SetupEventHandlers();
        }

        #region Public Methods

        /// <summary>Sets the frequency data to display</summary>
        public void SetFrequencies(List<FrequencyModulationInfo> frequencies)
        {
            _frequencies = frequencies ?? new List<FrequencyModulationInfo>();
            RefreshTreeView();
        }

        /// <summary>Clears all frequency data</summary>
        public void ClearFrequencies()
        {
            _frequencies.Clear();
            RefreshTreeView();
        }

        /// <summary>Expands all nodes in the tree view</summary>
        public void ExpandAll()
        {
            _treeView.ExpandAll();
        }

        /// <summary>Collapses all nodes in the tree view</summary>
        public void CollapseAll()
        {
            _treeView.CollapseAll();
        }

        /// <summary>Selects all checkboxes (if selection is enabled)</summary>
        public void SelectAll()
        {
            if (!_allowSelection) return;

            foreach (TreeNode coalitionNode in _treeView.Nodes)
            {
                coalitionNode.Checked = true;
                foreach (TreeNode freqNode in coalitionNode.Nodes)
                {
                    freqNode.Checked = true;
                }
            }
        }

        /// <summary>Clears all checkboxes (if selection is enabled)</summary>
        public void ClearSelection()
        {
            if (!_allowSelection) return;

            foreach (TreeNode coalitionNode in _treeView.Nodes)
            {
                coalitionNode.Checked = false;
                foreach (TreeNode freqNode in coalitionNode.Nodes)
                {
                    freqNode.Checked = false;
                }
            }
        }

        #endregion

        #region Control Creation

        private void CreateControls()
        {
            _containerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_containerPanel);

            CreateHeader();
            CreateTreeView();
        }

        private void CreateHeader()
        {
            _headerPanel = new Panel
            {
                Height = 25,
                Dock = DockStyle.Top,
                BackColor = SystemColors.Control,
                BorderStyle = BorderStyle.FixedSingle
            };

            _headerLabel = new Label
            {
                Text = "Frequencies",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = SystemColors.ControlDarkDark
            };
            _headerPanel.Controls.Add(_headerLabel);

            _containerPanel.Controls.Add(_headerPanel);
        }

        private void CreateTreeView()
        {
            _treeView = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = _allowSelection,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                FullRowSelect = true,
                HideSelection = false,
                Font = new Font("Segoe UI", 9F),
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.None
            };

            _containerPanel.Controls.Add(_treeView);
        }

        #endregion

        #region Event Handlers

        private void SetupEventHandlers()
        {
            _treeView.AfterCheck += OnTreeViewAfterCheck;
            _treeView.NodeMouseDoubleClick += OnTreeViewNodeMouseDoubleClick;
            _treeView.AfterSelect += OnTreeViewAfterSelect;
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
                else if (nodeType == "Frequency")
                {
                    // Update parent coalition node state
                    var parentNode = e.Node.Parent;
                    if (parentNode != null)
                    {
                        var hasCheckedFrequencies = parentNode.Nodes.Cast<TreeNode>().Any(n => n.Checked);
                        parentNode.Checked = hasCheckedFrequencies;
                    }
                }

                NodeChecked?.Invoke(this, e);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling tree view check change");
            }
        }

        private void OnTreeViewNodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            NodeDoubleClicked?.Invoke(this, e);
        }

        private void OnTreeViewAfterSelect(object? sender, TreeViewEventArgs e)
        {
            NodeSelected?.Invoke(this, e);
        }

        #endregion

        #region Tree Population

        private void RefreshTreeView()
        {
            _treeView.BeginUpdate();
            _treeView.Nodes.Clear();

            if (!_frequencies.Any())
            {
                var noDataNode = new TreeNode("No frequency data available")
                {
                    ForeColor = SystemColors.GrayText
                };
                _treeView.Nodes.Add(noDataNode);
                _treeView.EndUpdate();
                return;
            }

            PopulateTreeView();
            _treeView.EndUpdate();
        }

        private void PopulateTreeView()
        {
            // Group frequencies by coalition
            var coalitionGroups = _frequencies
                .SelectMany(f => f.Players.Select(p => new { Frequency = f, Player = p }))
                .GroupBy(x => x.Player.Coalition?.ToString() ?? "Unknown")
                .OrderBy(g => g.Key);

            foreach (var coalitionGroup in coalitionGroups)
            {
                var coalitionName = GetCoalitionName(coalitionGroup.Key);
                CreateCoalitionNode(coalitionName, coalitionGroup);
            }

            // Expand coalition nodes by default
            foreach (TreeNode coalitionNode in _treeView.Nodes)
            {
                coalitionNode.Expand();
            }
        }

        private void CreateCoalitionNode(string coalitionName, IGrouping<string, dynamic> coalitionGroup)
        {
            var totalFrequencies = coalitionGroup.GroupBy(x => x.Frequency).Count();
            var totalPlayers = coalitionGroup.Select(x => x.Player).DistinctBy(p => p.Name).Count();
            
            var nodeText = _showPlayerDetails 
                ? $"{coalitionName} ({totalFrequencies} freq, {totalPlayers} players)"
                : $"{coalitionName} ({totalFrequencies} frequencies)";
            
            var coalitionNode = new TreeNode(nodeText)
            {
                Tag = new { Type = "Coalition", Name = coalitionName, Data = coalitionGroup.ToList() },
                ForeColor = GetCoalitionColor(coalitionName),
                NodeFont = new Font(_treeView.Font, FontStyle.Bold),
                Checked = _allowSelection
            };

            var frequencyGroups = coalitionGroup
                .GroupBy(x => x.Frequency)
                .OrderBy(fg => fg.Key.Frequency);

            foreach (var freqGroup in frequencyGroups)
            {
                CreateFrequencyNode(coalitionNode, freqGroup);
            }

            _treeView.Nodes.Add(coalitionNode);
        }

        private void CreateFrequencyNode(TreeNode coalitionNode, IGrouping<dynamic, dynamic> freqGroup)
        {
            var freq = freqGroup.Key;
            var players = freqGroup.Select(x => x.Player).Distinct().ToList();
            
            var freqText = $"{freq.Frequency:F1} MHz ({freq.GetModulationName()}) - {players.Count} users";
            
            var freqNode = new TreeNode(freqText)
            {
                Tag = new { Type = "Frequency", Frequency = freq, Players = players },
                Checked = _allowSelection,
                ForeColor = SystemColors.WindowText
            };

            // Add player nodes if detailed view is enabled
            if (_showPlayerDetails)
            {
                foreach (var player in players.OrderBy(p => p.Name))
                {
                    CreatePlayerNode(freqNode, player, freq);
                }
            }

            coalitionNode.Nodes.Add(freqNode);
        }

        private void CreatePlayerNode(TreeNode freqNode, dynamic player, dynamic freq)
        {
            var duration = player.LastSeen - player.FirstSeen;
            var playerText = $"{player.Name} ({player.Aircraft}) - {player.PacketCount:N0} packets";
            
            if (duration.TotalSeconds > 0)
            {
                playerText += $", {FormatDuration(duration)}";
            }
            
            var playerNode = new TreeNode(playerText)
            {
                Tag = new { Type = "Player", Player = player, Frequency = freq },
                ForeColor = SystemColors.GrayText,
                NodeFont = new Font(_treeView.Font, FontStyle.Regular)
            };

            freqNode.Nodes.Add(playerNode);
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
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            else if (duration.TotalMinutes >= 1)
                return $"{duration.Minutes}m {duration.Seconds}s";
            else
                return $"{duration.Seconds}s";
        }

        #endregion

        private void InitializeComponent()
        {
            SuspendLayout();
            
            Name = "FrequencyTreeViewComponent";
            Size = new Size(300, 400);
            
            ResumeLayout(false);
        }
    }
}