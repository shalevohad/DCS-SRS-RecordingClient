using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Components;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Examples
{
    /// <summary>
    /// Example demonstrating the enhanced UI features in the DCS SRS Recording Player
    /// </summary>
    public class EnhancedUIFeaturesExample
    {
        /// <summary>
        /// Demonstrates the enhanced UI features available in the player
        /// </summary>
        public static void ShowEnhancedFeatures()
        {
            // The enhanced UI features have been implemented in the following areas:

            Console.WriteLine("=== Enhanced DCS SRS Recording Player UI Features ===\n");

            Console.WriteLine("1. Enhanced Player Controls - Modern playback interface with seeking:");
            Console.WriteLine("   - Modern circular play/pause/stop buttons with hover effects");
            Console.WriteLine("   - Enhanced waveform seek bar with real-time position display");
            Console.WriteLine("   - Time labels showing current position and duration");
            Console.WriteLine("   - Smooth seeking with visual feedback");
            Console.WriteLine();

            Console.WriteLine("2. Real-time Waveform Visualization - Visual representation of audio:");
            Console.WriteLine("   - WaveformSeekBar with frequency filtering");
            Console.WriteLine("   - Zoom in/out capabilities (mouse wheel + Ctrl)");
            Console.WriteLine("   - Time labels at start/end of visible range");
            Console.WriteLine("   - Current time label following playback position");
            Console.WriteLine("   - Visual progress overlay");
            Console.WriteLine();

            Console.WriteLine("3. Advanced Frequency Management - Tree-view based frequency selection:");
            Console.WriteLine("   - FrequencyFilterControl with coalition grouping");
            Console.WriteLine("   - Hierarchical display: Coalition > Frequency > Players");
            Console.WriteLine("   - Select All/None buttons for quick selection");
            Console.WriteLine("   - Expand/Collapse functionality");
            Console.WriteLine("   - Real-time filter status display");
            Console.WriteLine();

            Console.WriteLine("4. Live Audio Analysis - Real-time analysis display during playback:");
            Console.WriteLine("   - LiveAnalysisComponent with configurable analysis window");
            Console.WriteLine("   - Frequency Activity chart (bar chart of most active frequencies)");
            Console.WriteLine("   - Player Activity chart (bar chart of most active players)");
            Console.WriteLine("   - Modulation Activity chart (pie chart of modulation types)");
            Console.WriteLine("   - Statistics panel with real-time metrics");
            Console.WriteLine();

            Console.WriteLine("5. Enhanced File Management - Recent files, bookmarks, and favorites:");
            Console.WriteLine("   - RecentFilesComponent with tabbed interface");
            Console.WriteLine("   - Recent Files tab with file info and quick access");
            Console.WriteLine("   - Bookmarks tab for saving/navigating to specific positions");
            Console.WriteLine("   - Favorites tab for frequently accessed files");
            Console.WriteLine("   - Context menus for file operations");
            Console.WriteLine();

            Console.WriteLine("=== Keyboard Shortcuts ===");
            Console.WriteLine("   - Spacebar: Play/Pause");
            Console.WriteLine("   - Escape: Stop playback");
            Console.WriteLine("   - Ctrl+B: Add bookmark at current position");
            Console.WriteLine("   - Ctrl+E: Toggle enhanced features panel");
            Console.WriteLine("   - Ctrl+O: Open file (global shortcut)");
            Console.WriteLine();

            Console.WriteLine("=== Enhanced Player Models ===");
            ShowPlayerModelsInfo();
        }

        private static void ShowPlayerModelsInfo()
        {
            Console.WriteLine("New data models for enhanced functionality:\n");

            // Demonstrate RecentFileInfo
            var recentFile = new RecentFileInfo(
                @"C:\Recordings\mission_2024-01-15.srs",
                "mission_2024-01-15.srs",
                DateTime.Now.AddHours(-2),
                TimeSpan.FromMinutes(45),
                1250
            );
            Console.WriteLine($"RecentFileInfo example: {recentFile.DisplayName}");
            Console.WriteLine($"  Duration: {recentFile.FormattedDuration}");
            Console.WriteLine($"  Last accessed: {recentFile.FormattedLastAccessed}");
            Console.WriteLine($"  Packets: {recentFile.PacketCount}");
            Console.WriteLine();

            // Demonstrate AudioBookmark
            var bookmark = new AudioBookmark(
                @"C:\Recordings\mission_2024-01-15.srs",
                TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(30)),
                "Enemy contact at waypoint 3",
                DateTime.Now.AddMinutes(-30)
            );
            Console.WriteLine($"AudioBookmark example: {bookmark.Description}");
            Console.WriteLine($"  Position: {bookmark.FormattedPosition}");
            Console.WriteLine($"  Created: {bookmark.FormattedCreated}");
            Console.WriteLine();

            // Demonstrate LiveAnalysisStats
            var analysisStats = new LiveAnalysisStats(
                850,
                new System.Collections.Generic.Dictionary<double, int>
                {
                    { 251.0, 45 },
                    { 305.0, 32 },
                    { 127.5, 28 }
                },
                new System.Collections.Generic.Dictionary<string, int>
                {
                    { "Viper-1", 25 },
                    { "Eagle-2", 18 },
                    { "Hawg-3", 12 }
                },
                new System.Collections.Generic.Dictionary<string, int>
                {
                    { "AM", 42 },
                    { "FM", 13 }
                },
                TimeSpan.FromMinutes(2),
                7.08
            );
            Console.WriteLine($"LiveAnalysisStats example:");
            Console.WriteLine($"  Processed packets: {analysisStats.ProcessedPackets}");
            Console.WriteLine($"  Active frequencies: {analysisStats.FrequencyActivity.Count}");
            Console.WriteLine($"  Active players: {analysisStats.PlayerActivity.Count}");
            Console.WriteLine($"  Avg packets/sec: {analysisStats.AveragePacketsPerSecond:F2}");
            Console.WriteLine();

            // Demonstrate WaveformData
            var waveformData = new WaveformData(
                new float[] { 0.1f, 0.3f, 0.8f, 0.6f, 0.2f },
                new float[] { 0.05f, 0.2f, 0.6f, 0.4f, 0.1f },
                TimeSpan.FromMinutes(45),
                48000
            );
            Console.WriteLine($"WaveformData example:");
            Console.WriteLine($"  Duration: {waveformData.Duration}");
            Console.WriteLine($"  Sample rate: {waveformData.SampleRate} Hz");
            Console.WriteLine($"  Samples per pixel: {waveformData.SamplesPerPixel}");
            Console.WriteLine();
        }

        /// <summary>
        /// Example of how to integrate the enhanced components into a Windows Forms application
        /// </summary>
        public static Form CreateEnhancedPlayerDemo()
        {
            var form = new Form
            {
                Text = "Enhanced DCS SRS Player Demo",
                Size = new System.Drawing.Size(1200, 800),
                StartPosition = FormStartPosition.CenterScreen
            };

            // Create tab control for different enhanced features
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9F)
            };

            // Recent Files & Bookmarks Tab
            var filesTab = new TabPage("Files & Bookmarks")
            {
                BackColor = System.Drawing.Color.FromArgb(250, 252, 255)
            };

            var recentFilesComponent = new RecentFilesComponent
            {
                Dock = DockStyle.Fill
            };

            // Add some sample data
            recentFilesComponent.RecentFiles = new System.Collections.Generic.List<RecentFileInfo>
            {
                new RecentFileInfo(@"C:\Recordings\mission_2024-01-15.srs", "mission_2024-01-15.srs", 
                    DateTime.Now.AddHours(-1), TimeSpan.FromMinutes(45), 1250),
                new RecentFileInfo(@"C:\Recordings\training_2024-01-14.srs", "training_2024-01-14.srs", 
                    DateTime.Now.AddHours(-5), TimeSpan.FromMinutes(28), 890),
                new RecentFileInfo(@"C:\Recordings\sortie_2024-01-13.srs", "sortie_2024-01-13.srs", 
                    DateTime.Now.AddDays(-1), TimeSpan.FromMinutes(67), 1580)
            };

            recentFilesComponent.Bookmarks = new System.Collections.Generic.List<AudioBookmark>
            {
                new AudioBookmark(@"C:\Recordings\mission_2024-01-15.srs", 
                    TimeSpan.FromMinutes(15), "Enemy contact", DateTime.Now.AddMinutes(-30)),
                new AudioBookmark(@"C:\Recordings\mission_2024-01-15.srs", 
                    TimeSpan.FromMinutes(32), "Weapons hot", DateTime.Now.AddMinutes(-20)),
                new AudioBookmark(@"C:\Recordings\training_2024-01-14.srs", 
                    TimeSpan.FromMinutes(5), "Formation briefing", DateTime.Now.AddHours(-5))
            };

            filesTab.Controls.Add(recentFilesComponent);

            // Live Analysis Tab
            var analysisTab = new TabPage("Live Analysis")
            {
                BackColor = System.Drawing.Color.FromArgb(250, 252, 255)
            };

            var liveAnalysisComponent = new LiveAnalysisComponent
            {
                Dock = DockStyle.Fill
            };

            // Configure analysis with sample data
            liveAnalysisComponent.Config = new AnalysisConfig(
                true, true, true, true, TimeSpan.FromSeconds(30));

            analysisTab.Controls.Add(liveAnalysisComponent);

            tabControl.TabPages.AddRange(new TabPage[] { filesTab, analysisTab });
            form.Controls.Add(tabControl);

            return form;
        }
    }

    /// <summary>
    /// Demo runner for the enhanced UI features
    /// Call EnhancedUIFeaturesExample.ShowEnhancedFeatures() to see feature overview
    /// Call EnhancedUIFeaturesExample.CreateEnhancedPlayerDemo() to get a demo form
    /// </summary>
    public static class EnhancedUIDemo
    {
        /// <summary>
        /// Runs the enhanced UI demo (call this from your main program)
        /// </summary>
        public static void RunDemo()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Show feature overview in console
            EnhancedUIFeaturesExample.ShowEnhancedFeatures();

            // Create and show the demo form
            var demoForm = EnhancedUIFeaturesExample.CreateEnhancedPlayerDemo();
            Application.Run(demoForm);
        }
    }
}