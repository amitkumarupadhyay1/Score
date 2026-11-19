using System;
using System.Windows.Media;

namespace Score
{
    public class QuizThemePalette
    {
        public string Name { get; set; }
        public Brush WindowBackground { get; set; }
        public Brush HeaderBackground { get; set; }
        public Brush CardBackground { get; set; }
        public Brush TextPrimary { get; set; }
        public Brush TextSecondary { get; set; }
        public Brush AccentColor { get; set; }
        public Brush AccentBadgeBackground { get; set; }
        public Brush AccentBadgeForeground { get; set; }
        public Brush TimerBorderBackground { get; set; }
        public Brush TimerTextBrush { get; set; }
        public Brush OptionCardBackground { get; set; }
        public Brush OptionCardBorder { get; set; }
        public Brush OptionTextBrush { get; set; }
        public Brush ScoreCardBackground { get; set; }

        public static QuizThemePalette GetPalette(string themeName)
        {
            var brushConv = new BrushConverter();
            Brush Parse(string hex) => (Brush)brushConv.ConvertFromString(hex);

            switch (themeName?.ToLowerInvariant())
            {
                case "emerald":
                case "emerald arena":
                    return new QuizThemePalette
                    {
                        Name = "Emerald Arena",
                        WindowBackground = Parse("#FF064E3B"),
                        HeaderBackground = Parse("#FF065F46"),
                        CardBackground = Parse("#FFFFFFFF"),
                        TextPrimary = Parse("#FF064E3B"),
                        TextSecondary = Parse("#FF047857"),
                        AccentColor = Parse("#FF10B981"),
                        AccentBadgeBackground = Parse("#FFD1FAE5"),
                        AccentBadgeForeground = Parse("#FF065F46"),
                        TimerBorderBackground = Parse("#FF022C22"),
                        TimerTextBrush = Parse("#FF6EE7B7"),
                        OptionCardBackground = Parse("#FFF0FDF4"),
                        OptionCardBorder = Parse("#FFA7F3D0"),
                        OptionTextBrush = Parse("#FF064E3B"),
                        ScoreCardBackground = Parse("#FF065F46")
                    };

                case "ocean":
                case "cobalt":
                case "royal":
                case "neon":
                case "blue":
                    return new QuizThemePalette
                    {
                        Name = "Cobalt Ocean",
                        WindowBackground = Parse("#FF0F172A"),
                        HeaderBackground = Parse("#FF1E293B"),
                        CardBackground = Parse("#FFFFFFFF"),
                        TextPrimary = Parse("#FF0F172A"),
                        TextSecondary = Parse("#FF005A9C"),
                        AccentColor = Parse("#FF005A9C"),
                        AccentBadgeBackground = Parse("#FFDCEBFA"),
                        AccentBadgeForeground = Parse("#FF004377"),
                        TimerBorderBackground = Parse("#FF0F172A"),
                        TimerTextBrush = Parse("#FF38BDF8"),
                        OptionCardBackground = Parse("#FFF8FAFC"),
                        OptionCardBorder = Parse("#FFA8C5E3"),
                        OptionTextBrush = Parse("#FF0F172A"),
                        ScoreCardBackground = Parse("#FF1E293B")
                    };

                case "school":
                case "light":
                case "daylight":
                case "daylight contrast":
                    return new QuizThemePalette
                    {
                        Name = "Daylight Contrast",
                        WindowBackground = Parse("#FFF1F5F9"),
                        HeaderBackground = Parse("#FFFFFFFF"),
                        CardBackground = Parse("#FFFFFFFF"),
                        TextPrimary = Parse("#FF0F172A"),
                        TextSecondary = Parse("#FF334155"),
                        AccentColor = Parse("#FF2563EB"),
                        AccentBadgeBackground = Parse("#FFDBEAFE"),
                        AccentBadgeForeground = Parse("#FF1E40AF"),
                        TimerBorderBackground = Parse("#FFE2E8F0"),
                        TimerTextBrush = Parse("#FF1E293B"),
                        OptionCardBackground = Parse("#FFF8FAFC"),
                        OptionCardBorder = Parse("#FFCBD5E1"),
                        OptionTextBrush = Parse("#FF0F172A"),
                        ScoreCardBackground = Parse("#FFFFFFFF")
                    };

                case "corporate":
                case "slate":
                    return new QuizThemePalette
                    {
                        Name = "Corporate Slate",
                        WindowBackground = Parse("#FF0F172A"),
                        HeaderBackground = Parse("#FF1E293B"),
                        CardBackground = Parse("#FFFFFFFF"),
                        TextPrimary = Parse("#FF0F172A"),
                        TextSecondary = Parse("#FF334155"),
                        AccentColor = Parse("#FF3B82F6"),
                        AccentBadgeBackground = Parse("#FFDBEAFE"),
                        AccentBadgeForeground = Parse("#FF1D4ED8"),
                        TimerBorderBackground = Parse("#FF1E293B"),
                        TimerTextBrush = Parse("#FF60A5FA"),
                        OptionCardBackground = Parse("#FFF8FAFC"),
                        OptionCardBorder = Parse("#FFCBD5E1"),
                        OptionTextBrush = Parse("#FF0F172A"),
                        ScoreCardBackground = Parse("#FF1E293B")
                    };

                case "broadcast":
                case "studio":
                    return new QuizThemePalette
                    {
                        Name = "Broadcast Gold",
                        WindowBackground = Parse("#FF000000"),
                        HeaderBackground = Parse("#FF111111"),
                        CardBackground = Parse("#FFFFFFFF"),
                        TextPrimary = Parse("#FF000000"),
                        TextSecondary = Parse("#FF1F2937"),
                        AccentColor = Parse("#FFEAB308"),
                        AccentBadgeBackground = Parse("#FFFEF08A"),
                        AccentBadgeForeground = Parse("#FF854D0E"),
                        TimerBorderBackground = Parse("#FF000000"),
                        TimerTextBrush = Parse("#FFEAB308"),
                        OptionCardBackground = Parse("#FFF9FAFB"),
                        OptionCardBorder = Parse("#FFE5E7EB"),
                        OptionTextBrush = Parse("#FF111827"),
                        ScoreCardBackground = Parse("#FF111111")
                    };

                case "midnight":
                case "stage":
                case "classic":
                default:
                    return new QuizThemePalette
                    {
                        Name = "Midnight Navy",
                        WindowBackground = Parse("#FF0B132B"),
                        HeaderBackground = Parse("#FF1C2541"),
                        CardBackground = Parse("#FFFFFFFF"),
                        TextPrimary = Parse("#FF0F172A"),
                        TextSecondary = Parse("#FF334155"),
                        AccentColor = Parse("#FF00F2FE"),
                        AccentBadgeBackground = Parse("#FF3A506B"),
                        AccentBadgeForeground = Parse("#FF6FFFE9"),
                        TimerBorderBackground = Parse("#FF0B132B"),
                        TimerTextBrush = Parse("#FF6FFFE9"),
                        OptionCardBackground = Parse("#FFF8FAFC"),
                        OptionCardBorder = Parse("#FFE2E8F0"),
                        OptionTextBrush = Parse("#FF1E293B"),
                        ScoreCardBackground = Parse("#FF1C2541")
                    };
            }
        }
    }
}
