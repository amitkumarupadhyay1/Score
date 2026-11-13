using System;
using System.Windows.Media;

namespace Score
{
    internal enum ProjectorView { Live, Standings }
    internal enum ProjectorTheme { Dark, HighContrast, Light }

    internal sealed class ProjectorTeam
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public int RoundScore { get; set; }
        public long Total { get; set; }
        public int Rank { get; set; }
        public bool Eligible { get; set; }
    }

    internal sealed class ProjectorPalette
    {
        public Brush Background { get; private set; }
        public Brush Surface { get; private set; }
        public Brush Text { get; private set; }
        public Brush Secondary { get; private set; }
        public Brush Border { get; private set; }
        public Brush Accent { get; private set; }
        public Brush Highlight { get; private set; }

        public static ProjectorPalette For(ProjectorTheme theme)
        {
            if (theme == ProjectorTheme.HighContrast)
                return Create("#000000", "#000000", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFF00", "#000000");
            if (theme == ProjectorTheme.Light)
                return Create("#F1F5F9", "#FFFFFF", "#101828", "#344054", "#667085", "#654500", "#FFF4CE");
            return Create("#080F1C", "#15243B", "#FFFFFF", "#E4EDFF", "#8096B6", "#FFE082", "#233F61");
        }

        private static ProjectorPalette Create(string background, string surface, string text, string secondary, string border, string accent, string highlight)
        {
            return new ProjectorPalette { Background = ColorBrush(background), Surface = ColorBrush(surface), Text = ColorBrush(text), Secondary = ColorBrush(secondary), Border = ColorBrush(border), Accent = ColorBrush(accent), Highlight = ColorBrush(highlight) };
        }

        private static Brush ColorBrush(string color)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color);
            brush.Freeze();
            return brush;
        }
    }

    internal sealed class ProjectorLayout
    {
        public bool UsesRows { get; private set; }
        public int Columns { get; private set; }
        public int Rows { get; private set; }

        public static ProjectorLayout Choose(double width, double height, int count, ProjectorView view)
        {
            count = Math.Max(1, count);
            var rows = view == ProjectorView.Standings || count > 6;
            if (rows)
            {
                var columns = count > 6 && width / Math.Max(1, height) >= 1.5 ? 2 : 1;
                return new ProjectorLayout { UsesRows = true, Columns = columns, Rows = (int)Math.Ceiling(count / (double)columns) };
            }
            var bestColumns = 1;
            var bestScore = 0d;
            for (var columns = 1; columns <= Math.Min(4, count); columns++)
            {
                var rowCount = (int)Math.Ceiling(count / (double)columns);
                var cellWidth = width / columns - 12;
                var cellHeight = height / rowCount - 12;
                var score = Math.Min(cellWidth / 1.6, cellHeight) * Math.Sqrt(count / (double)(columns * rowCount));
                if (score > bestScore) { bestScore = score; bestColumns = columns; }
            }
            return new ProjectorLayout { UsesRows = false, Columns = bestColumns, Rows = (int)Math.Ceiling(count / (double)bestColumns) };
        }
    }
}
