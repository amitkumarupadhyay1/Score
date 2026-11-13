using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Score.Tests
{
    internal static class ProjectorChecks
    {
        internal static void Run()
        {
            var projector = new ProjectorWindow(new TextBox { Text = "JINGLE BELL SCHOOL · HISTORY HUNTERS" },
                new TextBlock { Text = "ROUND 2 OF 4" }, new TextBlock { Text = "04:35" },
                new TextBlock { Text = "QUESTION 8 · RUNNING · 00:15" }, new TextBlock { Text = "LIVE" });
            try
            {
                var scenarios = 0;
                foreach (var size in new[] { new Size(1280, 720), new Size(1366, 768), new Size(1920, 1080), new Size(3840, 2160), new Size(1024, 768), new Size(720, 1280) })
                    foreach (var count in new[] { 2, 6, 12 })
                        foreach (ProjectorView view in Enum.GetValues(typeof(ProjectorView)))
                            foreach (ProjectorTheme theme in Enum.GetValues(typeof(ProjectorTheme)))
                            {
                                projector.SetTeams(Teams(count), "matrix/" + count, "LIVE TOTALS");
                                projector.SetView(view);
                                projector.SetTheme(theme);
                                Arrange(projector, size);
                                Verify(projector, count, size);
                                if (size.Width == 1280 && size.Height == 720 && view == ProjectorView.Live)
                                    Require(projector.CurrentLayout.UsesRows == (count > 6), "Live layout did not adapt to team count.");
                                if (size.Width == 720 && count == 2 && view == ProjectorView.Live)
                                    Require(projector.CurrentLayout.Columns == 1, "Portrait layout did not stack two teams.");
                                if (size.Width == 1280 && size.Height == 720 && (count == 6 || count == 12))
                                    Capture(projector, "projector-" + count + "-" + view + "-" + theme + ".png");
                                scenarios++;
                            }
                Console.WriteLine("PASS  " + scenarios + " projector layouts: resolutions, team counts, views and themes");

                var original = Teams(12);
                projector.SetView(ProjectorView.Live);
                projector.SetTeams(original, "flash/1", "LIVE TOTALS");
                Arrange(projector, new Size(1280, 720));
                var before = Surfaces(projector).Select(tile => (string)tile.Tag).ToArray();
                var updated = Teams(12);
                updated[4].RoundScore += 5;
                updated[4].Total += 5;
                updated[4].Rank = 1;
                projector.SetTeams(updated, "flash/1", "LIVE TOTALS");
                Arrange(projector, new Size(1280, 720));
                Require(projector.IsHighlighted(updated[4].Key), "A changed score was not highlighted.");
                Require(!projector.IsHighlighted(updated[0].Key), "An unchanged team flashed.");
                Require(before.SequenceEqual(Surfaces(projector).Select(tile => (string)tile.Tag)), "Live teams moved after scoring.");
                Pump(1350);
                Require(!projector.IsHighlighted(updated[4].Key), "Score highlight did not expire.");

                projector.SetView(ProjectorView.Standings);
                Arrange(projector, new Size(1280, 720));
                Require(Surfaces(projector).Select(tile => (string)tile.Tag).SequenceEqual(updated.OrderBy(team => team.Rank).Select(team => team.Key)), "Standings are not sorted by rank.");
                projector.SetTheme(ProjectorTheme.HighContrast);
                Require(projector.View == ProjectorView.Standings, "A theme change replaced standings.");
                projector.SetTeams(Teams(2), "new-game/1", "LIVE TOTALS");
                Arrange(projector, new Size(1280, 720));
                Require(Surfaces(projector).Count() == 2 && !projector.IsHighlighted("team-4"), "Old teams or highlights survived a new game.");

                var extremes = Teams(2);
                extremes[0].Name = new string('W', 30);
                extremes[0].RoundScore = int.MinValue;
                extremes[0].Total = -21474836480L;
                extremes[1].RoundScore = int.MaxValue;
                extremes[1].Total = 21474836470L;
                foreach (var view in new[] { ProjectorView.Live, ProjectorView.Standings })
                {
                    projector.SetTeams(extremes, "large/1", "LIVE TOTALS");
                    projector.SetView(view);
                    Arrange(projector, new Size(1280, 720));
                    Verify(projector, 2, new Size(1280, 720));
                }
                Console.WriteLine("PASS  Projector stable positions, highlight expiry, ranked standings and large scores");

                projector.PresentationRoot.ClearValue(FrameworkElement.WidthProperty);
                projector.PresentationRoot.ClearValue(FrameworkElement.HeightProperty);
                projector.ShowInTaskbar = false;
                projector.Show();
                projector.UpdateLayout();
                var button = Descendants(projector).OfType<Button>().Single(item => item.Name == "ProjectorThemeButton");
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                var choice = button.ContextMenu.Items.OfType<MenuItem>().Single(item => (ProjectorTheme)item.Tag == ProjectorTheme.Light);
                choice.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                button.ContextMenu.IsOpen = false;
                Require(projector.Theme == ProjectorTheme.Light, "The projector theme menu did not apply Light.");
                Console.WriteLine("PASS  Projector window theme menu");
            }
            finally { projector.Close(); }
        }

        private static ProjectorTeam[] Teams(int count)
        {
            var names = new[] { "Jingle Bell History Hunters", "Chandra and the Explorers", "Ashoka History Society", "Maharana Pratap House", "The Archaeology Society", "Vikramaditya Scholars", "Saraswati Knowledge Club", "Chandragupta Researchers", "The Heritage Explorers", "Nalanda Young Historians", "Team Eleven from Ayodhya", "The Ancient World Scholars" };
            var values = Enumerable.Range(0, count).Select(i => new ProjectorTeam { Key = "team-" + i, Name = names[i], RoundScore = i == 0 ? -25 : 125 + i * 5, Total = i < 2 ? 250 : 225 - i * 5, Eligible = true }).ToArray();
            foreach (var team in values) team.Rank = 1 + values.Count(other => other.Total > team.Total);
            return values;
        }

        private static void Arrange(ProjectorWindow projector, Size size)
        {
            var root = projector.PresentationRoot;
            root.Width = size.Width;
            root.Height = size.Height;
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();
            root.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();
        }

        private static void Verify(ProjectorWindow projector, int count, Size size)
        {
            var root = projector.PresentationRoot;
            var surfaces = Surfaces(projector).ToArray();
            Require(surfaces.Length == count, "Some teams are missing.");
            var rectangles = surfaces.Select(surface => surface.TransformToAncestor(root).TransformBounds(new Rect(surface.RenderSize))).ToArray();
            for (var i = 0; i < rectangles.Length; i++)
            {
                var bounds = rectangles[i];
                Require(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= size.Width + 1 && bounds.Bottom <= size.Height + 1,
                    "Team outside projector at " + size + ", " + count + " teams, " + projector.View);
                for (var j = i + 1; j < rectangles.Length; j++) Require(!bounds.IntersectsWith(rectangles[j]), "Projected teams overlap.");
                foreach (var text in Descendants(surfaces[i]).OfType<TextBlock>())
                {
                    var box = text.TransformToAncestor(surfaces[i]).TransformBounds(new Rect(text.RenderSize));
                    Require(box.Left >= -1 && box.Right <= surfaces[i].ActualWidth + 1 && box.Top >= -1 && box.Bottom <= surfaces[i].ActualHeight + 1,
                        text.Name + " exceeds its tile at " + size + ", " + count + " teams, " + projector.View);
                    var formatted = new FormattedText(text.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, text.Foreground, 1);
                    if (text.TextWrapping == TextWrapping.Wrap) formatted.MaxTextWidth = Math.Max(1, text.ActualWidth);
                    Require(formatted.Height <= text.ActualHeight + 2 && formatted.Width <= text.ActualWidth + 2,
                        text.Name + " text is clipped at " + size + ": " + text.Text);
                    Require(Contrast(text.Foreground, surfaces[i].Background) >= 7, "Insufficient projected text contrast in " + projector.Theme);
                }
            }
            if (size.Width == 1280 && size.Height == 720 && count == 12)
            {
                Require(surfaces.SelectMany(Descendants).OfType<TextBlock>().Where(text => text.Name == "ProjectedName").All(text => text.FontSize >= 26), "Twelve-team names became too small at 720p.");
                Require(surfaces.SelectMany(Descendants).OfType<TextBlock>().Where(text => text.Name == "ProjectedValue").All(text => text.FontSize >= 45), "Primary scores became too small at 720p.");
            }
        }

        private static double Contrast(Brush first, Brush second)
        {
            var a = Luminance(((SolidColorBrush)first).Color);
            var b = Luminance(((SolidColorBrush)second).Color);
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }

        private static double Luminance(Color color)
        {
            Func<byte, double> linear = value => { var v = value / 255d; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); };
            return linear(color.R) * 0.2126 + linear(color.G) * 0.7152 + linear(color.B) * 0.0722;
        }

        private static IEnumerable<Border> Surfaces(ProjectorWindow projector) { return Descendants(projector.PresentationRoot).OfType<Border>().Where(border => border.Name == "ProjectedTeam"); }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                yield return child;
                foreach (var descendant in Descendants(child)) yield return descendant;
            }
        }

        private static void Capture(ProjectorWindow projector, string file)
        {
            var root = projector.PresentationRoot;
            var image = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var output = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file))) encoder.Save(output);
        }

        private static void Pump(int milliseconds)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
