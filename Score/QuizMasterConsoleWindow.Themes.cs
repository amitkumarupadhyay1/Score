using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Score
{
    public partial class QuizMasterConsoleWindow
    {
        private bool _isApplyingTheme = false;

        private void InitializeThemeSelector()
        {
            if (DeskThemeSelector == null) return;
            _isApplyingTheme = true;
            DeskThemeSelector.Items.Clear();
            DeskThemeSelector.Items.Add("Midnight Navy");
            DeskThemeSelector.Items.Add("Emerald Arena");
            DeskThemeSelector.Items.Add("Royal Velvet");
            DeskThemeSelector.Items.Add("Daylight Contrast");

            var currentTheme = _currentDeck?.Theme ?? "Midnight Navy";
            var match = DeskThemeSelector.Items.Cast<string>()
                .FirstOrDefault(item => item.IndexOf(currentTheme, StringComparison.OrdinalIgnoreCase) >= 0);

            DeskThemeSelector.SelectedItem = match ?? "Midnight Navy";
            _isApplyingTheme = false;
        }

        private void DeskThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingTheme || !(DeskThemeSelector.SelectedItem is string themeName)) return;

            ApplyTheme(themeName);
        }

        public void ApplyTheme(string themeName)
        {
            try
            {
                var palette = QuizThemePalette.GetPalette(themeName);
                if (_currentDeck != null)
                {
                    _currentDeck.Theme = palette.Name;
                    if (_currentSession != null)
                    {
                        var dbDeck = _db.QuizDecks.FirstOrDefault(d => d.QuizDeckId == _currentDeck.QuizDeckId);
                        if (dbDeck != null)
                        {
                            dbDeck.Theme = palette.Name;
                            _db.SaveChanges();
                        }
                    }
                }

                // Apply to live audience projector window
                if (_audienceWindow != null && _audienceWindow.IsLoaded)
                {
                    _audienceWindow.ApplyTheme(palette.Name);
                }

                ConsoleStatusText.Text = $"Theme '{palette.Name}' applied to stage and desk.";
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not apply quiz theme.", ex);
            }
        }
    }
}
