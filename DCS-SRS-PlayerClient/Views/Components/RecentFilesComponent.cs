using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ShalevOhad.DCS.SRS.Recorder.PlayerClient.Models;
using ShalevOhad.DCS.SRS.Recorder.Core.Settings;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.Views.Components
{
    /// <summary>
    /// Enhanced component for managing recent files, bookmarks, and favorites
    /// </summary>
    public class RecentFilesComponent : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Fields

        private TabControl _tabControl;
        private ListView _recentFilesListView;
        private ListView _bookmarksListView;
        private ListView _favoritesListView;
        private ContextMenuStrip _recentFilesContextMenu;
        private ContextMenuStrip _bookmarksContextMenu;
        private ContextMenuStrip _favoritesContextMenu;
        private PlayerSettingsStore _settingsStore;

        private List<RecentFileInfo> _recentFiles = new();
        private List<AudioBookmark> _bookmarks = new();
        private List<string> _favoriteFiles = new();

        #endregion

        #region Events

        public event EventHandler<string>? FileSelected;
        public event EventHandler<AudioBookmark>? BookmarkSelected;
        public event EventHandler<string>? StatusChanged;

        #endregion

        #region Properties

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<RecentFileInfo> RecentFiles
        {
            get => _recentFiles;
            set
            {
                _recentFiles = value ?? new();
                UpdateRecentFilesList();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<AudioBookmark> Bookmarks
        {
            get => _bookmarks;
            set
            {
                _bookmarks = value ?? new();
                UpdateBookmarksList();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<string> FavoriteFiles
        {
            get => _favoriteFiles;
            set
            {
                _favoriteFiles = value ?? new();
                UpdateFavoritesList();
            }
        }

        #endregion

        #region Constructor

        public RecentFilesComponent()
        {
            InitializeComponent();
            _settingsStore = PlayerSettingsStore.Instance;
            CreateControls();
            SetupContextMenus();
            LoadSavedData();
        }

        #endregion

        #region Initialization

        private void CreateControls()
        {
            // Modern styling
            BackColor = Color.FromArgb(245, 250, 255);
            
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Padding = new Point(8, 4),
                Appearance = TabAppearance.Normal,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(100, 28)
            };

            // Recent Files Tab
            var recentTab = new TabPage("Recent Files")
            {
                BackColor = Color.FromArgb(250, 252, 255),
                UseVisualStyleBackColor = true
            };
            
            _recentFilesListView = CreateStyledListView();
            _recentFilesListView.Columns.AddRange(new[]
            {
                new ColumnHeader { Text = "Name", Width = 200 },
                new ColumnHeader { Text = "Duration", Width = 80 },
                new ColumnHeader { Text = "Packets", Width = 80 },
                new ColumnHeader { Text = "Last Accessed", Width = 120 }
            });
            
            recentTab.Controls.Add(_recentFilesListView);

            // Bookmarks Tab
            var bookmarksTab = new TabPage("Bookmarks")
            {
                BackColor = Color.FromArgb(250, 252, 255),
                UseVisualStyleBackColor = true
            };
            
            _bookmarksListView = CreateStyledListView();
            _bookmarksListView.Columns.AddRange(new[]
            {
                new ColumnHeader { Text = "Position", Width = 80 },
                new ColumnHeader { Text = "Description", Width = 150 },
                new ColumnHeader { Text = "File", Width = 180 },
                new ColumnHeader { Text = "Created", Width = 100 }
            });
            
            bookmarksTab.Controls.Add(_bookmarksListView);

            // Favorites Tab
            var favoritesTab = new TabPage("Favorites")
            {
                BackColor = Color.FromArgb(250, 252, 255),
                UseVisualStyleBackColor = true
            };
            
            _favoritesListView = CreateStyledListView();
            _favoritesListView.Columns.AddRange(new[]
            {
                new ColumnHeader { Text = "Name", Width = 200 },
                new ColumnHeader { Text = "Path", Width = 250 }
            });
            
            favoritesTab.Controls.Add(_favoritesListView);

            _tabControl.TabPages.AddRange(new[] { recentTab, bookmarksTab, favoritesTab });
            Controls.Add(_tabControl);

            // Setup event handlers
            _recentFilesListView.DoubleClick += OnRecentFileDoubleClick;
            _recentFilesListView.MouseClick += OnRecentFileMouseClick;
            _bookmarksListView.DoubleClick += OnBookmarkDoubleClick;
            _bookmarksListView.MouseClick += OnBookmarkMouseClick;
            _favoritesListView.DoubleClick += OnFavoriteFileDoubleClick;
            _favoritesListView.MouseClick += OnFavoriteFileMouseClick;
        }

        private ListView CreateStyledListView()
        {
            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                HideSelection = false,
                BackColor = Color.FromArgb(252, 254, 255),
                ForeColor = Color.FromArgb(40, 40, 50),
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };

            // Modern header styling
            listView.OwnerDraw = true;
            listView.DrawColumnHeader += OnListViewDrawColumnHeader;
            listView.DrawItem += OnListViewDrawItem;
            listView.DrawSubItem += OnListViewDrawSubItem;

            return listView;
        }

        private void SetupContextMenus()
        {
            // Recent Files Context Menu
            _recentFilesContextMenu = new ContextMenuStrip();
            _recentFilesContextMenu.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Open File", null, OnOpenRecentFile),
                new ToolStripMenuItem("Add to Favorites", null, OnAddToFavorites),
                new ToolStripSeparator(),
                new ToolStripMenuItem("Remove from Recent", null, OnRemoveFromRecent),
                new ToolStripMenuItem("Clear All Recent", null, OnClearAllRecent)
            });

            // Bookmarks Context Menu
            _bookmarksContextMenu = new ContextMenuStrip();
            _bookmarksContextMenu.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Go to Bookmark", null, OnGoToBookmark),
                new ToolStripMenuItem("Edit Description", null, OnEditBookmarkDescription),
                new ToolStripSeparator(),
                new ToolStripMenuItem("Delete Bookmark", null, OnDeleteBookmark)
            });

            // Favorites Context Menu
            _favoritesContextMenu = new ContextMenuStrip();
            _favoritesContextMenu.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Open File", null, OnOpenFavoriteFile),
                new ToolStripSeparator(),
                new ToolStripMenuItem("Remove from Favorites", null, OnRemoveFromFavorites)
            });

            _recentFilesListView.ContextMenuStrip = _recentFilesContextMenu;
            _bookmarksListView.ContextMenuStrip = _bookmarksContextMenu;
            _favoritesListView.ContextMenuStrip = _favoritesContextMenu;
        }

        #endregion

        #region Data Management

        private void LoadSavedData()
        {
            try
            {
                // Load recent files from settings
                // This would integrate with the existing settings system
                UpdateRecentFilesList();
                UpdateBookmarksList();
                UpdateFavoritesList();
                
                Logger.Debug("Loaded saved file management data");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading saved file management data");
            }
        }

        private void SaveData()
        {
            try
            {
                // Save data to settings
                // This would integrate with the existing settings system
                Logger.Debug("Saved file management data");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving file management data");
            }
        }

        public void AddRecentFile(RecentFileInfo recentFile)
        {
            var recentFiles = new List<RecentFileInfo>(_recentFiles);
            recentFiles.RemoveAll(rf => rf.FilePath.Equals(recentFile.FilePath, StringComparison.OrdinalIgnoreCase));
            recentFiles.Insert(0, recentFile);
            
            if (recentFiles.Count > 10)
            {
                recentFiles = recentFiles.Take(10).ToList();
            }
            
            RecentFiles = recentFiles;
            SaveData();
        }

        public void AddBookmark(AudioBookmark bookmark)
        {
            var bookmarks = new List<AudioBookmark>(_bookmarks) { bookmark };
            Bookmarks = bookmarks;
            SaveData();
        }

        public void AddToFavorites(string filePath)
        {
            if (!_favoriteFiles.Contains(filePath))
            {
                var favorites = new List<string>(_favoriteFiles) { filePath };
                FavoriteFiles = favorites;
                SaveData();
                StatusChanged?.Invoke(this, $"Added to favorites: {Path.GetFileName(filePath)}");
            }
        }

        #endregion

        #region UI Updates

        private void UpdateRecentFilesList()
        {
            _recentFilesListView.Items.Clear();
            
            foreach (var file in _recentFiles)
            {
                var item = new ListViewItem(file.DisplayName)
                {
                    Tag = file,
                    ForeColor = file.IsValid ? Color.FromArgb(40, 40, 50) : Color.FromArgb(150, 150, 150)
                };
                
                item.SubItems.AddRange(new[]
                {
                    file.FormattedDuration,
                    file.PacketCount.ToString(),
                    file.FormattedLastAccessed
                });
                
                _recentFilesListView.Items.Add(item);
            }
        }

        private void UpdateBookmarksList()
        {
            _bookmarksListView.Items.Clear();
            
            foreach (var bookmark in _bookmarks)
            {
                var item = new ListViewItem(bookmark.FormattedPosition)
                {
                    Tag = bookmark
                };
                
                item.SubItems.AddRange(new[]
                {
                    bookmark.Description,
                    Path.GetFileName(bookmark.FilePath),
                    bookmark.FormattedCreated
                });
                
                _bookmarksListView.Items.Add(item);
            }
        }

        private void UpdateFavoritesList()
        {
            _favoritesListView.Items.Clear();
            
            foreach (var filePath in _favoriteFiles)
            {
                var item = new ListViewItem(Path.GetFileName(filePath))
                {
                    Tag = filePath,
                    ForeColor = File.Exists(filePath) ? Color.FromArgb(40, 40, 50) : Color.FromArgb(150, 150, 150)
                };
                
                item.SubItems.Add(filePath);
                _favoritesListView.Items.Add(item);
            }
        }

        #endregion

        #region Event Handlers

        private void OnRecentFileDoubleClick(object? sender, EventArgs e)
        {
            if (_recentFilesListView.SelectedItems.Count > 0 &&
                _recentFilesListView.SelectedItems[0].Tag is RecentFileInfo recentFile &&
                recentFile.IsValid)
            {
                FileSelected?.Invoke(this, recentFile.FilePath);
            }
        }

        private void OnRecentFileMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var item = _recentFilesListView.GetItemAt(e.X, e.Y);
                if (item != null)
                {
                    item.Selected = true;
                }
            }
        }

        private void OnBookmarkDoubleClick(object? sender, EventArgs e)
        {
            if (_bookmarksListView.SelectedItems.Count > 0 &&
                _bookmarksListView.SelectedItems[0].Tag is AudioBookmark bookmark)
            {
                BookmarkSelected?.Invoke(this, bookmark);
            }
        }

        private void OnBookmarkMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var item = _bookmarksListView.GetItemAt(e.X, e.Y);
                if (item != null)
                {
                    item.Selected = true;
                }
            }
        }

        private void OnFavoriteFileDoubleClick(object? sender, EventArgs e)
        {
            if (_favoritesListView.SelectedItems.Count > 0 &&
                _favoritesListView.SelectedItems[0].Tag is string filePath &&
                File.Exists(filePath))
            {
                FileSelected?.Invoke(this, filePath);
            }
        }

        private void OnFavoriteFileMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var item = _favoritesListView.GetItemAt(e.X, e.Y);
                if (item != null)
                {
                    item.Selected = true;
                }
            }
        }

        // Context menu handlers
        private void OnOpenRecentFile(object? sender, EventArgs e)
        {
            if (_recentFilesListView.SelectedItems.Count > 0 &&
                _recentFilesListView.SelectedItems[0].Tag is RecentFileInfo recentFile)
            {
                FileSelected?.Invoke(this, recentFile.FilePath);
            }
        }

        private void OnAddToFavorites(object? sender, EventArgs e)
        {
            if (_recentFilesListView.SelectedItems.Count > 0 &&
                _recentFilesListView.SelectedItems[0].Tag is RecentFileInfo recentFile)
            {
                AddToFavorites(recentFile.FilePath);
            }
        }

        private void OnRemoveFromRecent(object? sender, EventArgs e)
        {
            if (_recentFilesListView.SelectedItems.Count > 0 &&
                _recentFilesListView.SelectedItems[0].Tag is RecentFileInfo recentFile)
            {
                var recentFiles = new List<RecentFileInfo>(_recentFiles);
                recentFiles.Remove(recentFile);
                RecentFiles = recentFiles;
                SaveData();
            }
        }

        private void OnClearAllRecent(object? sender, EventArgs e)
        {
            if (MessageBox.Show("Clear all recent files?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                RecentFiles = new List<RecentFileInfo>();
                SaveData();
            }
        }

        private void OnGoToBookmark(object? sender, EventArgs e)
        {
            if (_bookmarksListView.SelectedItems.Count > 0 &&
                _bookmarksListView.SelectedItems[0].Tag is AudioBookmark bookmark)
            {
                BookmarkSelected?.Invoke(this, bookmark);
            }
        }

        private void OnEditBookmarkDescription(object? sender, EventArgs e)
        {
            if (_bookmarksListView.SelectedItems.Count > 0 &&
                _bookmarksListView.SelectedItems[0].Tag is AudioBookmark bookmark)
            {
                // Simple approach - for now we'll just show a message
                // In a full implementation, you could create a custom dialog
                MessageBox.Show($"Edit bookmark: {bookmark.Description}", "Edit Bookmark", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnDeleteBookmark(object? sender, EventArgs e)
        {
            if (_bookmarksListView.SelectedItems.Count > 0 &&
                _bookmarksListView.SelectedItems[0].Tag is AudioBookmark bookmark)
            {
                if (MessageBox.Show($"Delete bookmark '{bookmark.Description}'?", "Confirm", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    var bookmarks = new List<AudioBookmark>(_bookmarks);
                    bookmarks.Remove(bookmark);
                    Bookmarks = bookmarks;
                    SaveData();
                }
            }
        }

        private void OnOpenFavoriteFile(object? sender, EventArgs e)
        {
            if (_favoritesListView.SelectedItems.Count > 0 &&
                _favoritesListView.SelectedItems[0].Tag is string filePath)
            {
                FileSelected?.Invoke(this, filePath);
            }
        }

        private void OnRemoveFromFavorites(object? sender, EventArgs e)
        {
            if (_favoritesListView.SelectedItems.Count > 0 &&
                _favoritesListView.SelectedItems[0].Tag is string filePath)
            {
                var favorites = new List<string>(_favoriteFiles);
                favorites.Remove(filePath);
                FavoriteFiles = favorites;
                SaveData();
            }
        }

        #endregion

        #region Custom Drawing

        private void OnListViewDrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            // Modern header styling
            using (var brush = new LinearGradientBrush(e.Bounds,
                Color.FromArgb(240, 245, 250), Color.FromArgb(220, 230, 240), LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            using (var pen = new Pen(Color.FromArgb(180, 190, 200)))
            {
                e.Graphics.DrawRectangle(pen, e.Bounds);
            }

            var textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, 
                new Font("Segoe UI", 8.25F, FontStyle.Bold),
                textRect, Color.FromArgb(60, 70, 80),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private void OnListViewDrawItem(object? sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void OnListViewDrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        #endregion

        private void InitializeComponent()
        {
            SuspendLayout();
            
            Name = "RecentFilesComponent";
            Size = new Size(500, 300);
            
            ResumeLayout(false);
        }
    }
}