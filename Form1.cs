using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using WpfMediaElement = System.Windows.Controls.MediaElement;
using WpfMediaState = System.Windows.Controls.MediaState;
using WpfStretch = System.Windows.Media.Stretch;
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Size = System.Drawing.Size;
using Point = System.Drawing.Point;
using PointF = System.Drawing.PointF;
using Rectangle = System.Drawing.Rectangle;
using Graphics = System.Drawing.Graphics;
using SolidBrush = System.Drawing.SolidBrush;
using Pen = System.Drawing.Pen;
using Bitmap = System.Drawing.Bitmap;
using CopyPixelOperation = System.Drawing.CopyPixelOperation;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using SmoothingMode = System.Drawing.Drawing2D.SmoothingMode;

namespace MediaPlayer;

public enum AspectRatioMode
{
    Auto,
    Ratio16_9,
    Ratio4_3,
    Ratio21_9,
    Fill,
    Zoom,
    Original
}

public partial class Form1 : Form
{
    private static readonly Color WindowColor = Color.FromArgb(17, 19, 24);
    private static readonly Color PanelColor = Color.FromArgb(24, 27, 34);
    private static readonly Color AccentColor = Color.FromArgb(103, 139, 255);
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aac", ".aiff", ".alac", ".ape", ".flac", ".m4a", ".m4b", ".mp3",
        ".oga", ".ogg", ".opus", ".wav", ".wma"
    };

    private readonly ToolTip _toolTip;
    private readonly ElementHost _elementHost;
    private readonly System.Windows.Controls.Grid _mediaGrid;
    private readonly WpfMediaElement _mediaElement;
    private readonly ListBox _playlist;
    private readonly Label _emptyPlaylistLabel;
    private readonly Label _trackTitle;
    private readonly Label _trackStatus;
    private readonly Label _audioTitle;
    private readonly Label _elapsedLabel;
    private readonly Label _durationLabel;
    private readonly Label _playlistCount;
    private readonly Label _volumeLabel;
    private readonly Button _playButton;
    private readonly Button _muteButton;
    private readonly Button _aspectRatioButton;
    private readonly Button _screenshotButton;
    private readonly Button _fullScreenButton;
    private readonly ContextMenuStrip _videoContextMenu;
    private readonly TrackBar _seekBar;
    private readonly TrackBar _volumeBar;
    private readonly System.Windows.Forms.Timer _playbackTimer;

    private TableLayoutPanel? _rootLayout;
    private TableLayoutPanel? _headerLayout;
    private TableLayoutPanel? _contentLayout;
    private TableLayoutPanel? _playlistPanel;
    private Panel? _videoHost;

    private int _currentIndex = -1;
    private bool _isPlaying;
    private bool _isSeeking;
    private bool _suppressPlaylistSelection;
    private bool _isMuted;
    private TimeSpan _duration = TimeSpan.Zero;
    private AspectRatioMode _aspectRatioMode = AspectRatioMode.Auto;

    // Fullscreen state
    private bool _isFullScreen;
    private FormWindowState _previousWindowState = FormWindowState.Normal;
    private FormBorderStyle _previousBorderStyle = FormBorderStyle.Sizable;
    private Rectangle _previousBounds;

    public Form1(string[]? initialFiles = null)
    {
        InitializeComponent();

        Text = "Frame - Media Player";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 560);
        ClientSize = new Size(1180, 740);
        BackColor = WindowColor;
        ForeColor = Color.FromArgb(237, 239, 244);
        Font = new Font("Segoe UI", 9F);
        AllowDrop = true;
        DoubleBuffered = true;

        _toolTip = new ToolTip();
        _videoContextMenu = new ContextMenuStrip();

        // Native Windows Media Engine (WPF MediaElement via ElementHost inside a Grid container for aspect ratio control)
        // ScrubbingEnabled = false is critical for smooth 1080p, 2K, and 4K 60fps video decoding
        _mediaElement = new WpfMediaElement
        {
            LoadedBehavior = WpfMediaState.Manual,
            UnloadedBehavior = WpfMediaState.Manual,
            Stretch = WpfStretch.Uniform,
            ScrubbingEnabled = false,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        _mediaElement.MediaOpened += MediaElement_MediaOpened;
        _mediaElement.MediaEnded += MediaElement_MediaEnded;
        _mediaElement.MediaFailed += MediaElement_MediaFailed;
        _mediaElement.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2)
            {
                BeginInvoke(new Action(ToggleFullScreen));
                e.Handled = true;
            }
        };
        _mediaElement.MouseRightButtonUp += (s, e) =>
        {
            BeginInvoke(new Action(() => _videoContextMenu.Show(Cursor.Position)));
            e.Handled = true;
        };

        _mediaGrid = new System.Windows.Controls.Grid
        {
            Background = System.Windows.Media.Brushes.Black
        };
        _mediaGrid.Children.Add(_mediaElement);
        _mediaGrid.SizeChanged += (_, _) => ApplyAspectRatio();

        _elementHost = new ElementHost
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            Child = _mediaGrid,
            ContextMenuStrip = _videoContextMenu
        };
        _elementHost.Resize += (_, _) => ApplyAspectRatio();
        _elementHost.DoubleClick += (_, _) => ToggleFullScreen();

        _audioTitle = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(8, 9, 12),
            ForeColor = Color.FromArgb(215, 220, 232),
            Font = new Font("Segoe UI Semibold", 18F),
            Text = "Your media, all in one place",
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = true
        };
        _audioTitle.DoubleClick += (_, _) => ToggleFullScreen();

        _playlist = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            ForeColor = Color.FromArgb(226, 229, 237),
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 48,
            IntegralHeight = false,
            HorizontalScrollbar = true
        };
        _playlist.DrawItem += DrawPlaylistItem;
        _playlist.SelectedIndexChanged += PlaylistSelectionChanged;
        _playlist.DoubleClick += (_, _) => PlaySelected();

        _emptyPlaylistLabel = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            ForeColor = Color.FromArgb(135, 142, 158),
            Text = "Your playlist is empty.\nAdd files or drop them here.",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9.5F),
            Visible = true
        };

        _trackTitle = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(245, 246, 250),
            Font = new Font("Segoe UI Semibold", 9.5F),
            Text = "Nothing playing",
            Margin = Padding.Empty
        };
        _trackStatus = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(135, 142, 158),
            Font = new Font("Segoe UI", 8F),
            Text = "Open a file to get started",
            Margin = Padding.Empty
        };

        // Clear, compact time labels
        _elapsedLabel = MakeTimeLabel("00:00", ContentAlignment.MiddleLeft);
        _durationLabel = MakeTimeLabel("00:00", ContentAlignment.MiddleRight);
        _playlistCount = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(135, 142, 158),
            Text = "0 tracks",
            TextAlign = ContentAlignment.MiddleRight
        };
        _volumeLabel = new Label
        {
            AutoSize = false,
            Width = 44,
            Height = 30,
            ForeColor = Color.FromArgb(220, 224, 235),
            Text = "80%",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 8.5F),
            Margin = new Padding(2, 2, 2, 0)
        };

        _seekBar = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 1000,
            Value = 0,
            TickStyle = TickStyle.None,
            BackColor = PanelColor,
            Enabled = false,
            Height = 24,
            Margin = new Padding(6, 0, 6, 0)
        };
        _seekBar.MouseDown += (_, _) => _isSeeking = true;
        _seekBar.MouseUp += (_, _) => FinishSeeking();
        _seekBar.KeyUp += (_, _) => FinishSeeking();

        // Volume bar boosted up to 150%
        _volumeBar = new TrackBar
        {
            Minimum = 0,
            Maximum = 150,
            Value = 80,
            TickStyle = TickStyle.None,
            BackColor = PanelColor,
            Width = 76,
            Height = 24,
            Margin = new Padding(2, 4, 2, 0)
        };
        _volumeBar.ValueChanged += (_, _) =>
        {
            _mediaElement.Volume = _volumeBar.Value / 100.0;
            _volumeLabel.Text = $"{_volumeBar.Value}%";
            if (_volumeBar.Value > 100)
            {
                _volumeLabel.ForeColor = Color.FromArgb(255, 175, 75);
                _toolTip.SetToolTip(_volumeLabel, $"Volume: {_volumeBar.Value}% (Boosted)");
            }
            else
            {
                _volumeLabel.ForeColor = Color.FromArgb(220, 224, 235);
                _toolTip.SetToolTip(_volumeLabel, $"Volume: {_volumeBar.Value}%");
            }
        };
        _mediaElement.Volume = _volumeBar.Value / 100.0;

        _playButton = MakeButton("", AccentColor, Color.White, 52);
        _playButton.Paint += PlayButton_Paint;
        _playButton.Click += (_, _) => TogglePlayback();
        _toolTip.SetToolTip(_playButton, "Play (Space)");

        _muteButton = MakeButton("Mute", PanelColor, Color.FromArgb(212, 216, 226), 48);
        _muteButton.Click += (_, _) => ToggleMute();
        _toolTip.SetToolTip(_muteButton, "Mute / Unmute (M)");

        _aspectRatioButton = MakeButton("Fit", PanelColor, Color.FromArgb(212, 216, 226), 42);
        _aspectRatioButton.Click += (_, _) => CycleAspectRatio();
        _aspectRatioButton.ContextMenuStrip = _videoContextMenu;
        _toolTip.SetToolTip(_aspectRatioButton, "Aspect Ratio: Auto / Fit (A)");

        _screenshotButton = MakeButton("", PanelColor, Color.FromArgb(212, 216, 226), 36);
        _screenshotButton.Paint += ScreenshotButton_Paint;
        _screenshotButton.Click += (_, _) => TakeScreenshot();
        _toolTip.SetToolTip(_screenshotButton, "Screenshot (Ctrl + S)");

        _fullScreenButton = MakeButton("", PanelColor, Color.FromArgb(212, 216, 226), 36);
        _fullScreenButton.Paint += FullScreenButton_Paint;
        _fullScreenButton.Click += (_, _) => ToggleFullScreen();
        _toolTip.SetToolTip(_fullScreenButton, "Full Screen (F / F11)");

        BuildContextMenu();

        _playbackTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _playbackTimer.Tick += (_, _) => UpdatePlaybackPosition();
        _playbackTimer.Start();

        BuildLayout();

        DragEnter += HandleDragEnter;
        DragDrop += HandleDragDrop;
        _elementHost.DragEnter += HandleDragEnter;
        _elementHost.DragDrop += HandleDragDrop;
        _audioTitle.DragEnter += HandleDragEnter;
        _audioTitle.DragDrop += HandleDragDrop;
        _playlist.DragEnter += HandleDragEnter;
        _playlist.DragDrop += HandleDragDrop;
        FormClosed += HandleFormClosed;

        if (initialFiles != null && initialFiles.Length > 0)
        {
            Shown += (_, _) => AddFiles(initialFiles);
        }
    }

    private void BuildLayout()
    {
        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18, 10, 18, 12)
        };
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        Controls.Add(_rootLayout);

        _headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        var brand = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = WindowColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        brand.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(245, 246, 250),
            Font = new Font("Segoe UI Semibold", 15F),
            Text = "FRAME",
            Margin = new Padding(0, 0, 0, 0)
        });
        brand.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(135, 142, 158),
            Font = new Font("Segoe UI", 8.5F),
            Text = "A simple home for your media",
            Margin = new Padding(0, 1, 0, 0)
        });
        _headerLayout.Controls.Add(brand, 0, 0);

        var openButton = MakeButton("Open files", AccentColor, Color.White, 110);
        openButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        openButton.Margin = new Padding(0, 4, 0, 0);
        openButton.Click += (_, _) => OpenFiles();
        _toolTip.SetToolTip(openButton, "Open Media (Ctrl+O)");
        _headerLayout.Controls.Add(openButton, 1, 0);
        _rootLayout.Controls.Add(_headerLayout, 0, 0);

        _contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowColor,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 8)
        };
        _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

        _videoHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            Margin = new Padding(0, 0, 14, 0),
            ContextMenuStrip = _videoContextMenu
        };
        _videoHost.Controls.Add(_elementHost);
        _videoHost.Controls.Add(_audioTitle);
        _audioTitle.ContextMenuStrip = _videoContextMenu;
        _audioTitle.BringToFront();
        _videoHost.DoubleClick += (_, _) => ToggleFullScreen();
        _videoHost.Resize += (_, _) => ApplyAspectRatio();
        _contentLayout.Controls.Add(_videoHost, 0, 0);

        _playlistPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12, 10, 12, 8)
        };
        _playlistPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        _playlistPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var playlistHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            ColumnCount = 2,
            RowCount = 1
        };
        playlistHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        playlistHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        playlistHeader.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(240, 241, 246),
            Font = new Font("Segoe UI Semibold", 10.5F),
            Text = "Up next"
        }, 0, 0);
        playlistHeader.Controls.Add(_playlistCount, 1, 0);
        _playlistPanel.Controls.Add(playlistHeader, 0, 0);

        var playlistHost = new Panel { Dock = DockStyle.Fill, BackColor = PanelColor };
        playlistHost.Controls.Add(_playlist);
        playlistHost.Controls.Add(_emptyPlaylistLabel);
        _emptyPlaylistLabel.BringToFront();
        _playlistPanel.Controls.Add(playlistHost, 0, 1);
        _contentLayout.Controls.Add(_playlistPanel, 1, 0);
        _rootLayout.Controls.Add(_contentLayout, 0, 1);

        // --- FOOTER CONTAINER (Two strictly isolated rows: Row 0 = Timeline, Row 1 = Controls) ---
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(14, 6, 14, 6),
            Margin = Padding.Empty
        };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Row 0: Timeline
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Row 1: Controls

        // Row 0: Full-width Timeline with Elapsed, SeekBar, and Duration side-by-side
        var timeline = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        timeline.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        timeline.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        timeline.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        timeline.Controls.Add(_elapsedLabel, 0, 0);
        timeline.Controls.Add(_seekBar, 1, 0);
        timeline.Controls.Add(_durationLabel, 2, 0);
        footer.Controls.Add(timeline, 0, 0);

        // Row 1: Controls Bar (Track Info | Centered Transport | Volume & Tools)
        var controlsBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 4, 0, 0)
        };
        controlsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        controlsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        controlsBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var trackInfo = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        trackInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        trackInfo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        trackInfo.Controls.Add(_trackTitle, 0, 0);
        trackInfo.Controls.Add(_trackStatus, 0, 1);
        controlsBar.Controls.Add(trackInfo, 0, 0);

        var transport = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.None,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        var prevButton = MakeButton("", PanelColor, Color.FromArgb(212, 216, 226), 38);
        prevButton.Paint += (_, e) => DrawPreviousIcon(e.Graphics, prevButton.ClientSize);
        prevButton.Click += (_, _) => PlayIndex(_currentIndex - 1);
        _toolTip.SetToolTip(prevButton, "Previous");
        transport.Controls.Add(prevButton);

        transport.Controls.Add(_playButton);

        var nextButton = MakeButton("", PanelColor, Color.FromArgb(212, 216, 226), 38);
        nextButton.Paint += (_, e) => DrawNextIcon(e.Graphics, nextButton.ClientSize);
        nextButton.Click += (_, _) => PlayIndex(_currentIndex + 1);
        _toolTip.SetToolTip(nextButton, "Next");
        transport.Controls.Add(nextButton);
        controlsBar.Controls.Add(transport, 1, 0);

        var volumeControls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        volumeControls.Controls.Add(_muteButton);
        volumeControls.Controls.Add(_volumeBar);
        volumeControls.Controls.Add(_volumeLabel);
        volumeControls.Controls.Add(_aspectRatioButton);
        volumeControls.Controls.Add(_screenshotButton);
        volumeControls.Controls.Add(_fullScreenButton);
        controlsBar.Controls.Add(volumeControls, 2, 0);

        footer.Controls.Add(controlsBar, 0, 1);
        _rootLayout.Controls.Add(footer, 0, 2);
    }

    private void OpenFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open media",
            Multiselect = true,
            CheckFileExists = true,
            Filter =
                "Media files|*.mp4;*.mov;*.m4v;*.mkv;*.avi;*.wmv;*.webm;*.flv;*.mpeg;*.mpg;*.ts;*.3gp;*.mp3;*.m4a;*.aac;*.flac;*.wav;*.ogg;*.opus;*.wma|" +
                "All files|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddFiles(dialog.FileNames);
        }
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        var added = 0;
        var missingFiles = new List<string>();
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                missingFiles.Add(path);
                continue;
            }

            _playlist.Items.Add(path);
            added++;
        }

        _emptyPlaylistLabel.Visible = _playlist.Items.Count == 0;
        _playlistCount.Text = $"{_playlist.Items.Count} {(_playlist.Items.Count == 1 ? "track" : "tracks")}";

        if (missingFiles.Count > 0)
        {
            MessageBox.Show(this,
                $"These items could not be added because they are not files that exist:\n\n{string.Join(Environment.NewLine, missingFiles.Take(5))}",
                "Unable to add media",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        if (_currentIndex < 0 && added > 0)
        {
            _suppressPlaylistSelection = true;
            _playlist.SelectedIndex = 0;
            _suppressPlaylistSelection = false;
            PlayIndex(0);
        }
    }

    private void PlaySelected()
    {
        if (_playlist.SelectedIndex >= 0)
        {
            PlayIndex(_playlist.SelectedIndex);
        }
    }

    private void PlayIndex(int index)
    {
        if (index < 0 || index >= _playlist.Items.Count)
        {
            return;
        }

        var path = (string)_playlist.Items[index]!;
        if (!File.Exists(path))
        {
            _trackStatus.Text = "This file is no longer available";
            MessageBox.Show(this, $"The file could not be found:\n{path}", "File unavailable",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _currentIndex = index;
            _suppressPlaylistSelection = true;
            _playlist.SelectedIndex = index;
            _suppressPlaylistSelection = false;

            _duration = TimeSpan.Zero;
            _seekBar.Enabled = false;
            _seekBar.Value = 0;
            _elapsedLabel.Text = "00:00";
            _durationLabel.Text = "00:00";

            var title = Path.GetFileNameWithoutExtension(path);
            _trackTitle.Text = title;
            _trackStatus.Text = "Opening...";

            var isAudioOnly = AudioExtensions.Contains(Path.GetExtension(path));
            if (isAudioOnly)
            {
                _audioTitle.Text = title;
                _audioTitle.Visible = true;
                _audioTitle.BringToFront();
            }
            else
            {
                // For video formats (MP4, MKV, AVI, etc.), immediately surface the hardware video view
                _audioTitle.Visible = false;
                _elementHost.Visible = true;
                _elementHost.BringToFront();
            }

            // Load media directly into native Windows engine
            _mediaElement.Source = new Uri(Path.GetFullPath(path));
            _mediaElement.Play();
            UpdatePlayState(true, "Playing");
        }
        catch (Exception exception)
        {
            _trackStatus.Text = "Unable to open this media";
            MessageBox.Show(this, $"Unable to play this file.\n\n{exception.Message}", "Playback error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void MediaElement_MediaOpened(object? sender, System.Windows.RoutedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => MediaElement_MediaOpened(sender, e)));
            return;
        }

        if (_mediaElement.NaturalDuration.HasTimeSpan)
        {
            _duration = _mediaElement.NaturalDuration.TimeSpan;
            _durationLabel.Text = FormatTime((long)_duration.TotalMilliseconds);
            _seekBar.Enabled = _duration.TotalMilliseconds > 0;
        }
        else
        {
            _duration = TimeSpan.Zero;
            _seekBar.Enabled = false;
        }

        if (_mediaElement.NaturalVideoWidth > 0 || _mediaElement.HasVideo)
        {
            _audioTitle.Visible = false;
            _elementHost.Visible = true;
            _elementHost.BringToFront();
        }

        ApplyAspectRatio();
        UpdatePlayState(true, "Playing");
    }

    private void MediaElement_MediaEnded(object? sender, System.Windows.RoutedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => MediaElement_MediaEnded(sender, e)));
            return;
        }

        if (_currentIndex + 1 < _playlist.Items.Count)
        {
            PlayIndex(_currentIndex + 1);
        }
        else
        {
            UpdatePlayState(false, "Finished");
        }
    }

    private void MediaElement_MediaFailed(object? sender, System.Windows.ExceptionRoutedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => MediaElement_MediaFailed(sender, e)));
            return;
        }

        UpdatePlayState(false, "This format or file could not be played");
    }

    private void TogglePlayback()
    {
        if (_currentIndex < 0)
        {
            OpenFiles();
            return;
        }

        if (_isPlaying)
        {
            _mediaElement.Pause();
            UpdatePlayState(false, "Paused");
        }
        else
        {
            _mediaElement.Play();
            UpdatePlayState(true, "Playing");
        }
    }

    private void ToggleMute()
    {
        _isMuted = !_isMuted;
        _mediaElement.IsMuted = _isMuted;
        _muteButton.Text = _isMuted ? "Unmute" : "Mute";
    }

    private void FinishSeeking()
    {
        if (!_isSeeking)
        {
            return;
        }

        _isSeeking = false;
        if (_duration.TotalMilliseconds > 0)
        {
            var targetMs = _duration.TotalMilliseconds * (_seekBar.Value / (double)_seekBar.Maximum);
            _mediaElement.Position = TimeSpan.FromMilliseconds(targetMs);
        }
    }

    private void SeekRelative(double seconds)
    {
        if (_duration.TotalMilliseconds <= 0)
        {
            return;
        }

        var current = _mediaElement.Position;
        var target = current + TimeSpan.FromSeconds(seconds);
        if (target < TimeSpan.Zero) target = TimeSpan.Zero;
        if (target > _duration) target = _duration;

        _mediaElement.Position = target;
        _elapsedLabel.Text = FormatTime((long)target.TotalMilliseconds);
        _seekBar.Value = (int)Math.Clamp(
            target.TotalMilliseconds * _seekBar.Maximum / _duration.TotalMilliseconds,
            _seekBar.Minimum,
            _seekBar.Maximum);
    }

    private void ChangeVolume(int delta)
    {
        var newVol = Math.Clamp(_volumeBar.Value + delta, 0, 150);
        _volumeBar.Value = newVol;
    }

    private void UpdatePlaybackPosition()
    {
        if (_duration.TotalMilliseconds <= 0 && _mediaElement.NaturalDuration.HasTimeSpan)
        {
            _duration = _mediaElement.NaturalDuration.TimeSpan;
            _durationLabel.Text = FormatTime((long)_duration.TotalMilliseconds);
            _seekBar.Enabled = _duration.TotalMilliseconds > 0;
        }

        if (_duration.TotalMilliseconds <= 0 || !_isPlaying || _isSeeking)
        {
            return;
        }

        var currentPosition = _mediaElement.Position;
        _elapsedLabel.Text = FormatTime((long)currentPosition.TotalMilliseconds);

        var value = (int)Math.Clamp(
            currentPosition.TotalMilliseconds * _seekBar.Maximum / _duration.TotalMilliseconds,
            _seekBar.Minimum,
            _seekBar.Maximum);
        _seekBar.Value = value;
    }

    // ProcessCmdKey guarantees arrow keys and shortcuts are processed before any child control captures them
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Left)
        {
            SeekRelative(-10);
            return true;
        }
        if (keyData == Keys.Right)
        {
            SeekRelative(10);
            return true;
        }
        if (keyData == Keys.Up)
        {
            ChangeVolume(5);
            return true;
        }
        if (keyData == Keys.Down)
        {
            ChangeVolume(-5);
            return true;
        }
        if (keyData == Keys.Space && !(ActiveControl is TextBoxBase))
        {
            TogglePlayback();
            return true;
        }
        if (keyData == (Keys.Control | Keys.O))
        {
            OpenFiles();
            return true;
        }
        if (keyData == (Keys.Control | Keys.S))
        {
            TakeScreenshot();
            return true;
        }
        if (keyData == Keys.F11 || keyData == Keys.F || keyData == (Keys.Alt | Keys.Enter))
        {
            ToggleFullScreen();
            return true;
        }
        if (keyData == Keys.Escape && _isFullScreen)
        {
            ExitFullScreen();
            return true;
        }
        if (keyData == Keys.M)
        {
            ToggleMute();
            return true;
        }
        if (keyData == Keys.A)
        {
            CycleAspectRatio();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    public void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            ExitFullScreen();
        }
        else
        {
            EnterFullScreen();
        }
    }

    private void EnterFullScreen()
    {
        if (_isFullScreen) return;

        _previousWindowState = WindowState;
        _previousBorderStyle = FormBorderStyle;
        _previousBounds = Bounds;

        SuspendLayout();
        _isFullScreen = true;

        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Normal;
        Bounds = Screen.FromControl(this).Bounds;

        if (_headerLayout != null) _headerLayout.Visible = false;
        if (_playlistPanel != null) _playlistPanel.Visible = false;
        if (_rootLayout != null)
        {
            _rootLayout.RowStyles[0].Height = 0;
            _rootLayout.Padding = new Padding(8, 0, 8, 8);
        }
        if (_contentLayout != null)
        {
            _contentLayout.ColumnStyles[1].Width = 0;
        }

        _toolTip.SetToolTip(_fullScreenButton, "Exit Full Screen (Esc / F11)");
        _fullScreenButton.Invalidate();
        ResumeLayout(true);
        ApplyAspectRatio();
    }

    private void ExitFullScreen()
    {
        if (!_isFullScreen) return;

        SuspendLayout();
        _isFullScreen = false;

        FormBorderStyle = _previousBorderStyle;
        WindowState = _previousWindowState;
        if (_previousWindowState == FormWindowState.Normal)
        {
            Bounds = _previousBounds;
        }

        if (_headerLayout != null) _headerLayout.Visible = true;
        if (_playlistPanel != null) _playlistPanel.Visible = true;
        if (_rootLayout != null)
        {
            _rootLayout.RowStyles[0].Height = 54;
            _rootLayout.Padding = new Padding(18, 10, 18, 12);
        }
        if (_contentLayout != null)
        {
            _contentLayout.ColumnStyles[1].Width = 288;
        }

        _toolTip.SetToolTip(_fullScreenButton, "Full Screen (F / F11)");
        _fullScreenButton.Invalidate();
        ResumeLayout(true);
        ApplyAspectRatio();
    }

    private void HandleDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void HandleDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddFiles(paths);
        }
    }

    private void PlaylistSelectionChanged(object? sender, EventArgs e)
    {
        if (!_suppressPlaylistSelection && _playlist.SelectedIndex >= 0)
        {
            PlayIndex(_playlist.SelectedIndex);
        }
    }

    private void DrawPlaylistItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0)
        {
            return;
        }

        var path = (string)_playlist.Items[e.Index]!;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? Color.FromArgb(48, 57, 78) : PanelColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        var titleBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 7, e.Bounds.Width - 16, 20);
        var detailBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 27, e.Bounds.Width - 16, 16);
        TextRenderer.DrawText(e.Graphics, Path.GetFileNameWithoutExtension(path), Font,
            titleBounds, selected ? Color.White : Color.FromArgb(222, 225, 233),
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, Path.GetExtension(path).TrimStart('.').ToUpperInvariant(), Font,
            detailBounds, Color.FromArgb(137, 145, 162),
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        e.DrawFocusRectangle();
    }

    private void HandleFormClosed(object? sender, FormClosedEventArgs e)
    {
        _playbackTimer.Stop();
        try
        {
            _mediaElement.Stop();
            _mediaElement.Close();
        }
        catch
        {
            // Ignore on shutdown
        }
    }

    private void UpdatePlayState(bool isPlaying, string? status = null)
    {
        _isPlaying = isPlaying;
        if (!string.IsNullOrEmpty(status))
        {
            _trackStatus.Text = status;
        }
        _toolTip.SetToolTip(_playButton, _isPlaying ? "Pause (Space)" : "Play (Space)");
        _playButton.Invalidate();
    }

    private void PlayButton_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.White);

        var w = _playButton.ClientSize.Width;
        var h = _playButton.ClientSize.Height;
        var cx = w / 2f;
        var cy = h / 2f;

        if (_isPlaying)
        {
            // Pause logo: two clean vertical bars
            float barWidth = 4f;
            float barHeight = 15f;
            float gap = 5f;

            float left1 = cx - gap / 2f - barWidth;
            float left2 = cx + gap / 2f;
            float top = cy - barHeight / 2f;

            e.Graphics.FillRectangle(brush, left1, top, barWidth, barHeight);
            e.Graphics.FillRectangle(brush, left2, top, barWidth, barHeight);
        }
        else
        {
            // Play logo: clean centered triangle pointing right
            float halfHeight = 8f;
            float triWidth = 14f;
            float left = cx - (triWidth / 2f) + 1.5f;

            var points = new PointF[]
            {
                new PointF(left, cy - halfHeight),
                new PointF(left, cy + halfHeight),
                new PointF(left + triWidth, cy)
            };
            e.Graphics.FillPolygon(brush, points);
        }
    }

    private void FullScreenButton_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(220, 224, 235), 2f);

        var cx = _fullScreenButton.ClientSize.Width / 2f;
        var cy = _fullScreenButton.ClientSize.Height / 2f;
        float s = 6.5f;

        if (_isFullScreen)
        {
            // Inward arrows/brackets (restore)
            e.Graphics.DrawLines(pen, new[] { new PointF(cx - s, cy - 2), new PointF(cx - 2, cy - 2), new PointF(cx - 2, cy - s) });
            e.Graphics.DrawLines(pen, new[] { new PointF(cx + s, cy - 2), new PointF(cx + 2, cy - 2), new PointF(cx + 2, cy - s) });
            e.Graphics.DrawLines(pen, new[] { new PointF(cx - s, cy + 2), new PointF(cx - 2, cy + 2), new PointF(cx - 2, cy + s) });
            e.Graphics.DrawLines(pen, new[] { new PointF(cx + s, cy + 2), new PointF(cx + 2, cy + 2), new PointF(cx + 2, cy + s) });
        }
        else
        {
            // Outward corner brackets (fullscreen)
            e.Graphics.DrawLines(pen, new[] { new PointF(cx - s + 4, cy - s), new PointF(cx - s, cy - s), new PointF(cx - s, cy - s + 4) });
            e.Graphics.DrawLines(pen, new[] { new PointF(cx + s - 4, cy - s), new PointF(cx + s, cy - s), new PointF(cx + s, cy - s + 4) });
            e.Graphics.DrawLines(pen, new[] { new PointF(cx - s + 4, cy + s), new PointF(cx - s, cy + s), new PointF(cx - s, cy + s - 4) });
            e.Graphics.DrawLines(pen, new[] { new PointF(cx + s - 4, cy + s), new PointF(cx + s, cy + s), new PointF(cx + s, cy + s - 4) });
        }
    }

    private static void DrawPreviousIcon(Graphics g, Size size)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.FromArgb(220, 224, 235));

        float cx = size.Width / 2f;
        float cy = size.Height / 2f;
        float barWidth = 2.5f;
        float barHeight = 13f;
        float triHeight = 6.5f;

        // Bar on left
        float barX = cx - 7f;
        g.FillRectangle(brush, barX, cy - barHeight / 2f, barWidth, barHeight);

        // Triangle pointing left
        var points = new PointF[]
        {
            new PointF(cx + 6f, cy - triHeight),
            new PointF(cx + 6f, cy + triHeight),
            new PointF(cx - 3.5f, cy)
        };
        g.FillPolygon(brush, points);
    }

    private static void DrawNextIcon(Graphics g, Size size)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.FromArgb(220, 224, 235));

        float cx = size.Width / 2f;
        float cy = size.Height / 2f;
        float barWidth = 2.5f;
        float barHeight = 13f;
        float triHeight = 6.5f;

        // Triangle pointing right
        var points = new PointF[]
        {
            new PointF(cx - 6f, cy - triHeight),
            new PointF(cx - 6f, cy + triHeight),
            new PointF(cx + 3.5f, cy)
        };
        g.FillPolygon(brush, points);

        // Bar on right
        float barX = cx + 4.5f;
        g.FillRectangle(brush, barX, cy - barHeight / 2f, barWidth, barHeight);
    }

    private static Label MakeTimeLabel(string text, ContentAlignment alignment) => new()
    {
        Dock = DockStyle.Fill,
        ForeColor = Color.FromArgb(242, 244, 250),
        Text = text,
        TextAlign = alignment,
        Font = new Font("Segoe UI Semibold", 9.5F)
    };

    private static Button MakeButton(string text, Color backColor, Color foreColor, int width) => new()
    {
        BackColor = backColor,
        ForeColor = foreColor,
        Text = text,
        Width = width,
        Height = 34,
        FlatStyle = FlatStyle.Flat,
        FlatAppearance = { BorderSize = 0 },
        Font = new Font("Segoe UI Semibold", 9F),
        Cursor = Cursors.Hand,
        UseVisualStyleBackColor = false,
        Margin = new Padding(4, 0, 4, 0)
    };

    private void ScreenshotButton_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(220, 224, 235), 1.6f);

        var cx = _screenshotButton.ClientSize.Width / 2f;
        var cy = _screenshotButton.ClientSize.Height / 2f;

        // Camera body
        float bodyW = 18f;
        float bodyH = 12f;
        float bodyX = cx - bodyW / 2f;
        float bodyY = cy - bodyH / 2f + 1.5f;
        e.Graphics.DrawRectangle(pen, bodyX, bodyY, bodyW, bodyH);

        // Lens circle
        float lensR = 3.5f;
        e.Graphics.DrawEllipse(pen, cx - lensR, cy - lensR + 1.5f, lensR * 2, lensR * 2);

        // Top bump / shutter button
        e.Graphics.DrawLine(pen, cx - 4f, bodyY - 2.5f, cx + 1f, bodyY - 2.5f);
    }

    private void TakeScreenshot()
    {
        if (_mediaElement.Source == null)
        {
            MessageBox.Show(this, "No video is currently playing to capture.", "Screenshot",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var screenRect = _elementHost.RectangleToScreen(_elementHost.ClientRectangle);
            if (screenRect.Width <= 0 || screenRect.Height <= 0)
            {
                return;
            }

            int captureX = screenRect.X;
            int captureY = screenRect.Y;
            int captureW = screenRect.Width;
            int captureH = screenRect.Height;

            // Crop letterboxing/pillarboxing black bars based on active aspect ratio or video dimensions
            int vw = _mediaElement.NaturalVideoWidth;
            int vh = _mediaElement.NaturalVideoHeight;
            int renderW = screenRect.Width;
            int renderH = screenRect.Height;

            if (!double.IsNaN(_mediaElement.Width) && !double.IsNaN(_mediaElement.Height) && _mediaElement.Width > 0 && _mediaElement.Height > 0)
            {
                renderW = (int)Math.Round(_mediaElement.Width);
                renderH = (int)Math.Round(_mediaElement.Height);
            }
            else if (vw > 0 && vh > 0 && _aspectRatioMode == AspectRatioMode.Auto)
            {
                double scale = Math.Min((double)screenRect.Width / vw, (double)screenRect.Height / vh);
                renderW = (int)Math.Round(vw * scale);
                renderH = (int)Math.Round(vh * scale);
            }

            int offsetX = Math.Max(0, (screenRect.Width - renderW) / 2);
            int offsetY = Math.Max(0, (screenRect.Height - renderH) / 2);

            captureX += offsetX;
            captureY += offsetY;
            captureW = Math.Max(1, Math.Min(renderW, screenRect.Width));
            captureH = Math.Max(1, Math.Min(renderH, screenRect.Height));

            using var bitmap = new Bitmap(captureW, captureH);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(new Point(captureX, captureY), Point.Empty, new Size(captureW, captureH), CopyPixelOperation.SourceCopy);
            }

            try
            {
                Clipboard.SetImage(bitmap);
            }
            catch
            {
                // Ignore clipboard contention
            }

            var currentTrackTitle = _currentIndex >= 0 && _currentIndex < _playlist.Items.Count
                ? Path.GetFileNameWithoutExtension((string)_playlist.Items[_currentIndex]!)
                : "VideoSnapshot";

            var timeStr = FormatTimeForFileName((long)_mediaElement.Position.TotalMilliseconds);
            var defaultFileName = $"{SanitizeFileName(currentTrackTitle)}_{timeStr}.png";
            var defaultDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            using var saveDialog = new SaveFileDialog
            {
                Title = "Save Video Screenshot",
                InitialDirectory = defaultDir,
                FileName = defaultFileName,
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp",
                DefaultExt = "png",
                AddExtension = true
            };

            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                var ext = Path.GetExtension(saveDialog.FileName).ToLowerInvariant();
                var format = ext switch
                {
                    ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                    ".bmp" => ImageFormat.Bmp,
                    _ => ImageFormat.Png
                };

                bitmap.Save(saveDialog.FileName, format);
                _trackStatus.Text = $"Saved: {Path.GetFileName(saveDialog.FileName)} (Copied to clipboard)";
            }
            else
            {
                _trackStatus.Text = "Screenshot copied to clipboard";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to capture screenshot:\n{ex.Message}", "Screenshot Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string FormatTimeForFileName(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return $"{(int)time.TotalHours:00}h{time.Minutes:00}m{time.Seconds:00}s";
    }

    private static string SanitizeFileName(string name)
    {
        var invalids = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalids.Contains(c) ? '_' : c));
    }

    private void CycleAspectRatio()
    {
        var modes = (AspectRatioMode[])Enum.GetValues(typeof(AspectRatioMode));
        var nextIndex = ((int)_aspectRatioMode + 1) % modes.Length;
        SetAspectRatio(modes[nextIndex]);
    }

    private void SetAspectRatio(AspectRatioMode mode)
    {
        _aspectRatioMode = mode;
        ApplyAspectRatio();
        _aspectRatioButton.Text = GetAspectRatioShortName(mode);
        _trackStatus.Text = $"Aspect Ratio: {GetAspectRatioName(mode)}";
        _toolTip.SetToolTip(_aspectRatioButton, $"Aspect Ratio: {GetAspectRatioName(mode)} (A)");
        UpdateContextMenuCheckmarks();
    }

    private void ApplyAspectRatio()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ApplyAspectRatio));
            return;
        }

        double containerW = _elementHost.ClientSize.Width;
        double containerH = _elementHost.ClientSize.Height;

        if (containerW <= 0 || containerH <= 0)
        {
            return;
        }

        switch (_aspectRatioMode)
        {
            case AspectRatioMode.Auto:
                _mediaElement.Width = double.NaN;
                _mediaElement.Height = double.NaN;
                _mediaElement.Stretch = WpfStretch.Uniform;
                break;

            case AspectRatioMode.Fill:
                _mediaElement.Width = double.NaN;
                _mediaElement.Height = double.NaN;
                _mediaElement.Stretch = WpfStretch.Fill;
                break;

            case AspectRatioMode.Zoom:
                _mediaElement.Width = double.NaN;
                _mediaElement.Height = double.NaN;
                _mediaElement.Stretch = WpfStretch.UniformToFill;
                break;

            case AspectRatioMode.Original:
                _mediaElement.Width = double.NaN;
                _mediaElement.Height = double.NaN;
                _mediaElement.Stretch = WpfStretch.None;
                break;

            case AspectRatioMode.Ratio16_9:
                ApplyFixedRatio(containerW, containerH, 16.0 / 9.0);
                break;

            case AspectRatioMode.Ratio4_3:
                ApplyFixedRatio(containerW, containerH, 4.0 / 3.0);
                break;

            case AspectRatioMode.Ratio21_9:
                ApplyFixedRatio(containerW, containerH, 21.0 / 9.0);
                break;
        }
    }

    private void ApplyFixedRatio(double containerW, double containerH, double targetRatio)
    {
        double containerRatio = containerW / containerH;
        double w, h;
        if (containerRatio > targetRatio)
        {
            h = containerH;
            w = containerH * targetRatio;
        }
        else
        {
            w = containerW;
            h = containerW / targetRatio;
        }

        _mediaElement.Width = Math.Round(w);
        _mediaElement.Height = Math.Round(h);
        _mediaElement.Stretch = WpfStretch.Fill;
    }

    private void BuildContextMenu()
    {
        _videoContextMenu.Items.Clear();

        var aspectMenu = new ToolStripMenuItem("Aspect Ratio");
        foreach (AspectRatioMode mode in Enum.GetValues(typeof(AspectRatioMode)))
        {
            var item = new ToolStripMenuItem(GetAspectRatioName(mode), null, (_, _) => SetAspectRatio(mode))
            {
                Checked = mode == _aspectRatioMode,
                Tag = mode
            };
            aspectMenu.DropDownItems.Add(item);
        }

        _videoContextMenu.Items.Add(aspectMenu);
        _videoContextMenu.Items.Add(new ToolStripSeparator());
        _videoContextMenu.Items.Add(new ToolStripMenuItem("Take Screenshot (Ctrl + S)", null, (_, _) => TakeScreenshot()));
        _videoContextMenu.Items.Add(new ToolStripMenuItem("Toggle Full Screen (F / F11)", null, (_, _) => ToggleFullScreen()));
    }

    private void UpdateContextMenuCheckmarks()
    {
        if (_videoContextMenu.Items.Count > 0 && _videoContextMenu.Items[0] is ToolStripMenuItem aspectMenu)
        {
            foreach (ToolStripItem item in aspectMenu.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Tag is AspectRatioMode mode)
                {
                    menuItem.Checked = mode == _aspectRatioMode;
                }
            }
        }
    }

    private static string GetAspectRatioShortName(AspectRatioMode mode) => mode switch
    {
        AspectRatioMode.Auto => "Fit",
        AspectRatioMode.Ratio16_9 => "16:9",
        AspectRatioMode.Ratio4_3 => "4:3",
        AspectRatioMode.Ratio21_9 => "21:9",
        AspectRatioMode.Fill => "Fill",
        AspectRatioMode.Zoom => "Zoom",
        AspectRatioMode.Original => "1:1",
        _ => "Fit"
    };

    private static string GetAspectRatioName(AspectRatioMode mode) => mode switch
    {
        AspectRatioMode.Auto => "Auto / Fit (Default)",
        AspectRatioMode.Ratio16_9 => "16:9 (Widescreen)",
        AspectRatioMode.Ratio4_3 => "4:3 (Classic TV)",
        AspectRatioMode.Ratio21_9 => "21:9 (Cinema Ultrawide)",
        AspectRatioMode.Fill => "Fill (Stretch)",
        AspectRatioMode.Zoom => "Zoom (Crop)",
        AspectRatioMode.Original => "Original (1:1)",
        _ => "Auto / Fit"
    };

    private static string FormatTime(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
    }
}
