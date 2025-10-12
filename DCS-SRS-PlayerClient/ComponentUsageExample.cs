using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Components;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient
{
    /// <summary>
    /// Example showing how to use the new reorganized components
    /// </summary>
    public partial class ComponentUsageExample : Form
    {
        private FileSelectionComponent _fileSelectionComponent;
        private FrequencyTreeViewComponent _frequencyTreeComponent;
        private PlayerComponent _playerComponent;
        private AnalyzerComponent _analyzerComponent;

        public ComponentUsageExample()
        {
            InitializeComponent();
            CreateExampleLayout();
            SetupExampleEvents();
        }

        private void CreateExampleLayout()
        {
            this.Size = new System.Drawing.Size(800, 600);
            this.Text = "Reorganized Components Example";

            // File selection at the top
            _fileSelectionComponent = new FileSelectionComponent
            {
                Dock = DockStyle.Top,
                Height = 55
            };
            this.Controls.Add(_fileSelectionComponent);

            // Tab control for different component examples
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            // Player component example
            var playerTab = new TabPage("Player Component");
            _playerComponent = new PlayerComponent
            {
                Dock = DockStyle.Fill
            };
            playerTab.Controls.Add(_playerComponent);
            tabControl.TabPages.Add(playerTab);

            // Analyzer component example
            var analyzerTab = new TabPage("Analyzer Component");
            _analyzerComponent = new AnalyzerComponent
            {
                Dock = DockStyle.Fill
            };
            analyzerTab.Controls.Add(_analyzerComponent);
            tabControl.TabPages.Add(analyzerTab);

            // Frequency tree component example
            var frequencyTab = new TabPage("Frequency Tree Component");
            _frequencyTreeComponent = new FrequencyTreeViewComponent
            {
                Dock = DockStyle.Fill,
                HeaderText = "Example Frequencies",
                ShowPlayerDetails = true,
                AllowSelection = true
            };
            frequencyTab.Controls.Add(_frequencyTreeComponent);
            tabControl.TabPages.Add(frequencyTab);

            this.Controls.Add(tabControl);
        }

        private void SetupExampleEvents()
        {
            // File selection events
            _fileSelectionComponent.FileSelected += (sender, filePath) =>
            {
                MessageBox.Show($"File selected: {filePath}", "File Selection");
                // Here you would normally load the file into the components
            };

            // Player component events
            _playerComponent.StatusChanged += (sender, status) =>
            {
                this.Text = $"Reorganized Components Example - {status}";
            };

            // Analyzer component events
            _analyzerComponent.StatusChanged += (sender, status) =>
            {
                this.Text = $"Reorganized Components Example - Analysis: {status}";
            };

            // Frequency tree events
            _frequencyTreeComponent.SelectionChanged += (sender, e) =>
            {
                var selectedCount = _frequencyTreeComponent.SelectedFrequencies.Count();
                MessageBox.Show($"Selected {selectedCount} frequencies", "Selection Changed");
            };

            _frequencyTreeComponent.NodeDoubleClicked += (sender, e) =>
            {
                if (e.Node?.Text != null)
                {
                    MessageBox.Show($"Double-clicked: {e.Node.Text}", "Node Double-Clicked");
                }
            };
        }

        /// <summary>
        /// Example of how to populate the frequency tree with sample data
        /// </summary>
        private void LoadSampleFrequencyData()
        {
            var sampleFrequencies = new List<FrequencyModulationInfo>
            {
                new FrequencyModulationInfo(251.0, (Modulation)0) // AM = 0
                {
                    Players = new List<PlayerFrequencyInfo>
                    {
                        new PlayerFrequencyInfo
                        {
                            Name = "Viper1",
                            Coalition = "2", // Blue
                            Aircraft = "F-16C_50",
                            PacketCount = 150,
                            FirstSeen = DateTime.UtcNow.AddMinutes(-10),
                            LastSeen = DateTime.UtcNow.AddMinutes(-2)
                        }
                    }
                },
                new FrequencyModulationInfo(305.0, (Modulation)1) // FM = 1
                {
                    Players = new List<PlayerFrequencyInfo>
                    {
                        new PlayerFrequencyInfo
                        {
                            Name = "Hawg1",
                            Coalition = "2", // Blue
                            Aircraft = "A-10C_2",
                            PacketCount = 89,
                            FirstSeen = DateTime.UtcNow.AddMinutes(-15),
                            LastSeen = DateTime.UtcNow.AddMinutes(-1)
                        }
                    }
                }
            };

            _frequencyTreeComponent.SetFrequencies(sampleFrequencies);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Name = "ComponentUsageExample";
            this.Text = "Reorganized Components Example";
            this.ResumeLayout(false);
        }
    }
}