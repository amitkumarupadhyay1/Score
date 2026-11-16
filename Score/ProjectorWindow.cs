using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Screen = System.Windows.Forms.Screen;

namespace Score
{
    internal sealed class ProjectorWindow : Window
    {
        private readonly Grid root = new Grid();
        private readonly Grid header = new Grid();
        private readonly Grid board = new Grid();
        private readonly TextBlock title = Text(30);
        private readonly TextBlock context = Text(24);
        private readonly TextBlock clock = Text(36);
        private readonly TextBlock question = Text(24);
        private readonly TextBlock caption = Text(18);
        private readonly Button themeButton = new Button { Name = "ProjectorThemeButton", Padding = new Thickness(12, 4, 12, 4), FontSize = 16, FontWeight = FontWeights.SemiBold, ToolTip = "Change projector theme (T). Live view: L. Standings: S. Fullscreen: F11. Close: Esc." };
        private readonly Dictionary<string, Tile> tiles = new Dictionary<string, Tile>();
        private readonly List<TextBlock> headings = new List<TextBlock>();
        private readonly Dictionary<string, long> highlights = new Dictionary<string, long>();
        private readonly Stopwatch highlightClock = Stopwatch.StartNew();
        private readonly DispatcherTimer highlightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        private ProjectorTeam[] teams = new ProjectorTeam[0];
        private ProjectorPalette palette = ProjectorPalette.For(ProjectorTheme.Dark);
        private string roundKey;
        private string totalsCaption = "TOURNAMENT TOTALS";
        private bool rebuilding;

        internal ProjectorView View { get; private set; }
        internal ProjectorTheme Theme { get; private set; }
        internal FrameworkElement PresentationRoot { get { return root; } }
        internal ProjectorLayout CurrentLayout { get; private set; }
        internal event Action<ProjectorTheme> ThemeChanged;

        public ProjectorWindow(TextBox eventTitle, TextBlock roundNumber, TextBlock roundClock, TextBlock questionSummary, TextBlock competitionStatus)
        {
            Title = "QuizScore Live – Projector";
            Width = 1100;
            Height = 700;
            MinWidth = 480;
            MinHeight = 320;
            UseLayoutRounding = true;
            WindowStyle = WindowStyle.None;
            ShowActivated = false;
            FontFamily = new FontFamily("Segoe UI");
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.4, GridUnitType.Star) });
            header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            title.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = eventTitle });
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            header.Children.Add(title);
            var contextBinding = new MultiBinding { StringFormat = "{0}  ·  {1}" };
            contextBinding.Bindings.Add(new Binding("Text") { Source = roundNumber });
            contextBinding.Bindings.Add(new Binding("Text") { Source = competitionStatus });
            context.SetBinding(TextBlock.TextProperty, contextBinding);
            context.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetRow(context, 1);
            header.Children.Add(context);
            clock.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = roundClock, StringFormat = "ROUND  {0}" });
            clock.TextAlignment = TextAlignment.Right;
            Grid.SetColumn(clock, 1);
            header.Children.Add(clock);
            question.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = questionSummary });
            question.TextAlignment = TextAlignment.Right;
            question.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetRow(question, 1);
            Grid.SetColumn(question, 1);
            header.Children.Add(question);
            root.Children.Add(header);
            Grid.SetRow(board, 1);
            root.Children.Add(board);
            var footer = new DockPanel { Margin = new Thickness(6, 6, 6, 0) };
            DockPanel.SetDock(themeButton, Dock.Right);
            footer.Children.Add(themeButton);
            caption.VerticalAlignment = VerticalAlignment.Center;
            caption.TextTrimming = TextTrimming.CharacterEllipsis;
            footer.Children.Add(caption);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            Content = root;
            root.SizeChanged += (sender, args) => ResizePresentation();
            board.SizeChanged += (sender, args) => Rebuild();
            themeButton.Click += ShowThemeMenu;
            var buttonBorder = new FrameworkElementFactory(typeof(Border));
            buttonBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            buttonBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            buttonBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            buttonBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            buttonBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            buttonBorder.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            themeButton.Template = new ControlTemplate(typeof(Button)) { VisualTree = buttonBorder };
            highlightTimer.Tick += (sender, args) => ExpireHighlights();
            MouseLeftButtonDown += (sender, args) =>
            {
                if (args.Handled) return;
                if (args.ClickCount == 2) ToggleFullscreen();
                else if (WindowState == WindowState.Normal) DragMove();
            };
            SetTheme(ProjectorTheme.Dark);
        }

        internal void SetView(ProjectorView view)
        {
            if (View == view && tiles.Count > 0) return;
            View = view;
            Rebuild();
        }

        internal void SetTheme(ProjectorTheme theme)
        {
            var changed = Theme != theme;
            Theme = theme;
            palette = ProjectorPalette.For(theme);
            Background = root.Background = palette.Background;
            title.Foreground = clock.Foreground = palette.Text;
            context.Foreground = caption.Foreground = palette.Secondary;
            question.Foreground = palette.Accent;
            themeButton.Content = "THEME · " + ThemeName(theme).ToUpperInvariant();
            themeButton.Foreground = palette.Text;
            themeButton.Background = palette.Surface;
            themeButton.BorderBrush = palette.Border;
            foreach (var tile in tiles.Values) Paint(tile);
            foreach (var label in headings) label.Foreground = palette.Secondary;
            if (changed && ThemeChanged != null) ThemeChanged(theme);
        }

        private static string ThemeName(ProjectorTheme theme) { return theme == ProjectorTheme.HighContrast ? "High contrast" : theme.ToString(); }

        private void ShowThemeMenu(object sender, RoutedEventArgs args)
        {
            var menu = new ContextMenu { PlacementTarget = themeButton, Placement = PlacementMode.Top };
            foreach (var theme in new[] { ProjectorTheme.HighContrast, ProjectorTheme.Dark, ProjectorTheme.Light })
            {
                var choice = theme;
                var item = new MenuItem { Header = ThemeName(theme), IsCheckable = true, IsChecked = Theme == theme, Tag = theme };
                item.Click += (s, e) => SetTheme(choice);
                menu.Items.Add(item);
            }
            themeButton.ContextMenu = menu;
            menu.IsOpen = true;
        }

        internal void SetTeams(ProjectorTeam[] values, string currentRoundKey, string standingsCaption)
        {
            var sameRound = roundKey == currentRoundKey;
            if (!sameRound) { highlights.Clear(); highlightTimer.Stop(); }
            var previous = teams.ToDictionary(team => team.Key);
            if (sameRound)
                foreach (var team in values)
                {
                    ProjectorTeam old;
                    if (previous.TryGetValue(team.Key, out old) && (old.RoundScore != team.RoundScore || old.Total != team.Total))
                        highlights[team.Key] = highlightClock.ElapsedMilliseconds + 1200;
                }
            var changedOrder = !teams.Select(team => team.Key).SequenceEqual(values.Select(team => team.Key));
            if (View == ProjectorView.Standings)
                changedOrder |= !teams.OrderBy(team => team.Rank).Select(team => team.Key).SequenceEqual(values.OrderBy(team => team.Rank).Select(team => team.Key));
            teams = values.ToArray();
            roundKey = currentRoundKey;
            totalsCaption = standingsCaption;
            if (changedOrder || tiles.Count != teams.Length) Rebuild();
            else foreach (var team in teams) UpdateTile(tiles[team.Key], team);
            UpdateCaption();
            if (highlights.Count > 0) highlightTimer.Start();
        }

        internal bool IsHighlighted(string key) { return highlights.ContainsKey(key); }

        private void ExpireHighlights()
        {
            foreach (var key in highlights.Where(entry => entry.Value <= highlightClock.ElapsedMilliseconds).Select(entry => entry.Key).ToArray())
            {
                highlights.Remove(key);
                Tile tile;
                if (tiles.TryGetValue(key, out tile)) Paint(tile);
            }
            if (highlights.Count == 0) highlightTimer.Stop();
        }

        private void ResizePresentation()
        {
            var scale = Math.Max(0.5, Math.Min(root.ActualWidth / 1280d, root.ActualHeight / 720d));
            var margin = Math.Max(12, 24 * scale);
            header.Margin = new Thickness(margin, margin * 0.6, margin, margin * 0.45);
            board.Margin = new Thickness(margin * 0.6, 0, margin * 0.6, 0);
            title.FontSize = 30 * scale;
            context.FontSize = question.FontSize = 20 * scale;
            clock.FontSize = 34 * scale;
            caption.FontSize = 18 * scale;
            themeButton.FontSize = Math.Max(12, 16 * scale);
            Rebuild();
        }

        private void UpdateCaption()
        {
            caption.Text = View == ProjectorView.Standings ? "STANDINGS · " + totalsCaption : "LIVE · ROUND SCORES";
        }

        private void Rebuild()
        {
            if (rebuilding || board.ActualWidth <= 0 || board.ActualHeight <= 0) return;
            rebuilding = true;
            try
            {
                CurrentLayout = ProjectorLayout.Choose(board.ActualWidth, board.ActualHeight, teams.Length, View);
                board.Children.Clear();
                board.RowDefinitions.Clear();
                board.ColumnDefinitions.Clear();
                tiles.Clear();
                headings.Clear();
                if (CurrentLayout.UsesRows) BuildRows();
                else BuildCards();
                UpdateCaption();
            }
            finally { rebuilding = false; }
        }

        private void BuildCards()
        {
            for (var i = 0; i < CurrentLayout.Columns; i++) board.ColumnDefinitions.Add(new ColumnDefinition());
            for (var i = 0; i < CurrentLayout.Rows; i++) board.RowDefinitions.Add(new RowDefinition());
            var width = board.ActualWidth / CurrentLayout.Columns;
            var height = board.ActualHeight / CurrentLayout.Rows;
            for (var i = 0; i < teams.Length; i++)
            {
                var tile = CreateTile(teams[i], width, height, false);
                Grid.SetRow(tile.Surface, i / CurrentLayout.Columns);
                Grid.SetColumn(tile.Surface, i % CurrentLayout.Columns);
                board.Children.Add(tile.Surface);
            }
        }

        private void BuildRows()
        {
            var ordered = View == ProjectorView.Standings ? teams.OrderBy(team => team.Rank).ToArray() : teams;
            var width = board.ActualWidth / CurrentLayout.Columns;
            var headingHeight = Math.Max(22, board.ActualHeight * 0.055);
            var height = (board.ActualHeight - headingHeight) / CurrentLayout.Rows;
            for (var column = 0; column < CurrentLayout.Columns; column++)
            {
                board.ColumnDefinitions.Add(new ColumnDefinition());
                var lane = new Grid { Margin = new Thickness(6, 0, 6, 0) };
                lane.RowDefinitions.Add(new RowDefinition { Height = new GridLength(headingHeight) });
                for (var i = 0; i < CurrentLayout.Rows; i++) lane.RowDefinitions.Add(new RowDefinition());
                var headingGrid = new Grid { Margin = new Thickness(14, 0, 14, 0) };
                AddRowColumns(headingGrid);
                var labels = View == ProjectorView.Standings ? new[] { "#", "TEAM", "TOTAL" } : new[] { "TEAM", "ROUND", "TOTAL" };
                for (var field = 0; field < labels.Length; field++)
                {
                    var heading = Text(Math.Max(12, headingHeight * 0.55));
                    heading.Text = labels[field];
                    heading.Foreground = palette.Secondary;
                    heading.TextAlignment = field == 2 || View == ProjectorView.Live && field == 1 ? TextAlignment.Right : TextAlignment.Left;
                    Grid.SetColumn(heading, field);
                    headingGrid.Children.Add(heading);
                    headings.Add(heading);
                }
                lane.Children.Add(headingGrid);
                for (var row = 0; row < CurrentLayout.Rows; row++)
                {
                    var index = column * CurrentLayout.Rows + row;
                    if (index >= ordered.Length) break;
                    var tile = CreateTile(ordered[index], width - 12, height, true);
                    Grid.SetRow(tile.Surface, row + 1);
                    lane.Children.Add(tile.Surface);
                }
                Grid.SetColumn(lane, column);
                board.Children.Add(lane);
            }
        }

        private Tile CreateTile(ProjectorTeam team, double width, double height, bool rows)
        {
            var tile = new Tile { Key = team.Key, IsRow = rows, Width = width, Height = height };
            var pad = Math.Max(4, Math.Min(width, height) * 0.035);
            tile.Surface = new Border { Tag = team.Key, Name = "ProjectedTeam", BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(rows ? 8 : 14), Padding = new Thickness(pad), Margin = new Thickness(rows ? 0 : 6, rows ? 3 : 6, rows ? 0 : 6, rows ? 3 : 6) };
            var grid = new Grid();
            tile.Name = Text(32); tile.Name.Name = "ProjectedName";
            tile.Name.TextWrapping = TextWrapping.Wrap;
            tile.Name.TextTrimming = TextTrimming.None;
            tile.Value = Text(80); tile.Value.Name = "ProjectedValue";
            tile.Total = Text(28); tile.Total.Name = "ProjectedTotal";
            tile.Rank = Text(36); tile.Rank.Name = "ProjectedRank";
            if (rows)
            {
                AddRowColumns(grid);
                if (View == ProjectorView.Standings)
                {
                    Grid.SetColumn(tile.Name, 1);
                    Grid.SetColumn(tile.Value, 2);
                    grid.Children.Add(tile.Rank);
                }
                else
                {
                    Grid.SetColumn(tile.Value, 1);
                    Grid.SetColumn(tile.Total, 2);
                    grid.Children.Add(tile.Total);
                }
                tile.Value.TextAlignment = tile.Total.TextAlignment = TextAlignment.Right;
                tile.Name.Margin = new Thickness(8, 0, 8, 0);
            }
            else
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.25, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.60, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.15, GridUnitType.Star) });
                tile.Name.TextAlignment = tile.Value.TextAlignment = tile.Total.TextAlignment = TextAlignment.Center;
                Grid.SetRow(tile.Value, 1);
                Grid.SetRow(tile.Total, 2);
                grid.Children.Add(tile.Total);
            }
            grid.Children.Add(tile.Name);
            grid.Children.Add(tile.Value);
            tile.Surface.Child = grid;
            tiles.Add(team.Key, tile);
            UpdateTile(tile, team);
            return tile;
        }

        private void AddRowColumns(Grid grid)
        {
            foreach (var weight in View == ProjectorView.Standings ? new[] { 0.10, 0.66, 0.24 } : new[] { 0.60, 0.23, 0.17 })
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) });
        }

        private void UpdateTile(Tile tile, ProjectorTeam team)
        {
            tile.Name.Text = string.IsNullOrWhiteSpace(team.Name) ? "Team" : team.Name;
            tile.Value.Text = View == ProjectorView.Standings ? team.Total.ToString(CultureInfo.InvariantCulture) : team.Eligible ? team.RoundScore.ToString(CultureInfo.InvariantCulture) : "—";
            tile.Total.Text = tile.IsRow ? team.Total.ToString(CultureInfo.InvariantCulture) : "TOTAL  " + team.Total.ToString(CultureInfo.InvariantCulture);
            tile.Rank.Text = team.Rank.ToString(CultureInfo.InvariantCulture);
            var surface = tile.Surface;
            var width = Math.Max(1, tile.Width - surface.Margin.Left - surface.Margin.Right - surface.Padding.Left - surface.Padding.Right - 6);
            var height = Math.Max(1, tile.Height - surface.Margin.Top - surface.Margin.Bottom - surface.Padding.Top - surface.Padding.Bottom - 6);
            if (tile.IsRow)
            {
                var standings = View == ProjectorView.Standings;
                Fit(tile.Name, Math.Min(height * 0.39, width * 0.065), width * (standings ? 0.66 : 0.60), height, true);
                Fit(tile.Value, height * 0.80, width * (standings ? 0.24 : 0.23), height, false);
                Fit(tile.Total, height * 0.45, width * 0.17, height, false);
                Fit(tile.Rank, height * 0.55, width * 0.10, height, false);
            }
            else
            {
                Fit(tile.Name, Math.Min(height * 0.105, width * 0.09), width, height * 0.25, true);
                Fit(tile.Value, height * 0.55, width, height * 0.60, false);
                Fit(tile.Total, height * 0.10, width, height * 0.15, false);
            }
            System.Windows.Automation.AutomationProperties.SetName(tile.Surface, team.Name + ", round " + team.RoundScore + ", total " + team.Total + ", rank " + team.Rank);
            Paint(tile);
        }

        private static void Fit(TextBlock text, double requestedSize, double width, double height, bool wrap)
        {
            width = Math.Max(1, width); height = Math.Max(1, height);
            text.FontSize = Math.Max(8, requestedSize);
            for (var attempt = 0; attempt < 20; attempt++)
            {
                text.Measure(new Size(wrap ? width : double.PositiveInfinity, double.PositiveInfinity));
                if (text.DesiredSize.Height <= height && (wrap || text.DesiredSize.Width <= width)) break;
                var factor = Math.Min(height / Math.Max(1, text.DesiredSize.Height), wrap ? 0.94 : width / Math.Max(1, text.DesiredSize.Width));
                text.FontSize = Math.Max(1, text.FontSize * Math.Min(0.94, factor));
            }
        }

        private void Paint(Tile tile)
        {
            var highlighted = highlights.ContainsKey(tile.Key);
            tile.Surface.Background = highlighted ? palette.Highlight : palette.Surface;
            tile.Surface.BorderBrush = highlighted ? palette.Accent : palette.Border;
            tile.Name.Foreground = tile.Value.Foreground = palette.Text;
            tile.Total.Foreground = palette.Secondary;
            tile.Rank.Foreground = palette.Accent;
        }

        public void ShowOnAudienceScreen(Window host)
        {
            var hostScreen = Screen.FromHandle(new WindowInteropHelper(host).Handle);
            var audienceScreen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName != hostScreen.DeviceName);
            if (audienceScreen == null) { WindowStartupLocation = WindowStartupLocation.CenterScreen; Show(); return; }
            WindowStartupLocation = WindowStartupLocation.Manual;
            SourceInitialized += (sender, args) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                // Use the monitor handle to get the true physical working area,
                // which is immune to DPI virtualisation on the primary display.
                var monitor = MonitorFromPoint(new POINT { X = audienceScreen.Bounds.Left + 1, Y = audienceScreen.Bounds.Top + 1 }, 2 /* MONITOR_DEFAULTTONEAREST */);
                var info = new MONITORINFO { Size = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (GetMonitorInfo(monitor, ref info))
                    SetWindowPos(handle, IntPtr.Zero, info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top, 0x0014);
                else
                    SetWindowPos(handle, IntPtr.Zero, audienceScreen.Bounds.Left, audienceScreen.Bounds.Top, audienceScreen.Bounds.Width, audienceScreen.Bounds.Height, 0x0014);
            };
            Show();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int Size;
            public RECT Monitor;
            public RECT WorkArea;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape) Close();
            else if (e.Key == Key.L) SetView(ProjectorView.Live);
            else if (e.Key == Key.S) SetView(ProjectorView.Standings);
            else if (e.Key == Key.T) SetTheme((ProjectorTheme)(((int)Theme + 1) % 3));
            else if (e.Key == Key.F11) ToggleFullscreen();
        }

        private void ToggleFullscreen() { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }

        protected override void OnClosed(EventArgs e)
        {
            highlightTimer.Stop();
            highlights.Clear();
            BindingOperations.ClearAllBindings(title);
            BindingOperations.ClearAllBindings(context);
            BindingOperations.ClearAllBindings(clock);
            BindingOperations.ClearAllBindings(question);
            base.OnClosed(e);
        }

        private static TextBlock Text(double size) { return new TextBlock { FontSize = size, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }; }

        private sealed class Tile
        {
            public string Key;
            public bool IsRow;
            public double Width;
            public double Height;
            public Border Surface;
            public TextBlock Name;
            public TextBlock Value;
            public TextBlock Total;
            public TextBlock Rank;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    }
}
