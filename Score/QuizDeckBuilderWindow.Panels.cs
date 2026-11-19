using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Score
{
    public partial class QuizDeckBuilderWindow : Window
    {
        private void CollapseLeftPanelButton_Click(object sender, RoutedEventArgs e)
        {
            LeftColumnDef.Width = new GridLength(0);
            ExpandLeftPanelButton.Visibility = Visibility.Visible;
            StatusText.Text = "Questions panel collapsed. Click [> QUESTIONS] to expand.";
        }

        private void ExpandLeftPanelButton_Click(object sender, RoutedEventArgs e)
        {
            LeftColumnDef.Width = new GridLength(280);
            ExpandLeftPanelButton.Visibility = Visibility.Collapsed;
            StatusText.Text = "Questions panel restored.";
        }

        private void CollapseRightPanelButton_Click(object sender, RoutedEventArgs e)
        {
            RightColumnDef.Width = new GridLength(0);
            ExpandRightPanelButton.Visibility = Visibility.Visible;
            StatusText.Text = "Simulator panel collapsed. Click [◀ SIMULATOR] to expand.";
        }

        private void ExpandRightPanelButton_Click(object sender, RoutedEventArgs e)
        {
            RightColumnDef.Width = new GridLength(320);
            ExpandRightPanelButton.Visibility = Visibility.Collapsed;
            StatusText.Text = "Simulator panel restored.";
        }

        private void DeckThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = DeckThemeSelector?.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selected)) return;

            if (_currentDeck != null)
            {
                _currentDeck.Theme = selected;
                if (!_loadingDeck)
                {
                    try
                    {
                        _db.SaveChanges();
                        StatusText.Text = $"Deck theme changed to {selected}.";
                    }
                    catch (Exception ex)
                    {
                        AppDiagnostics.WriteException("Could not persist deck theme change.", ex);
                    }
                }
            }

            ApplyThemeToSimulator(selected);
        }

        private void ApplyThemeToSimulator(string theme)
        {
            if (PreviewStageScreenBorder == null) return;
            var bc = new BrushConverter();

            switch (theme?.ToLowerInvariant())
            {
                case "school":
                    PreviewStageScreenBorder.Background = (Brush)bc.ConvertFromString("#FFF0F4F8");
                    PreviewStageScreenBorder.BorderBrush = (Brush)bc.ConvertFromString("#FF93C5FD");
                    break;
                case "corporate":
                    PreviewStageScreenBorder.Background = (Brush)bc.ConvertFromString("#FF1E293B");
                    PreviewStageScreenBorder.BorderBrush = (Brush)bc.ConvertFromString("#FF334155");
                    break;
                case "broadcast":
                    PreviewStageScreenBorder.Background = (Brush)bc.ConvertFromString("#FF000000");
                    PreviewStageScreenBorder.BorderBrush = (Brush)bc.ConvertFromString("#FFEAB308");
                    break;
                case "stage":
                default:
                    PreviewStageScreenBorder.Background = (Brush)bc.ConvertFromString("#FF0F172A");
                    PreviewStageScreenBorder.BorderBrush = (Brush)bc.ConvertFromString("#FF1E293B");
                    break;
            }

            if (!_loadingEditor)
            {
                UpdatePreview();
            }
        }
    }
}
