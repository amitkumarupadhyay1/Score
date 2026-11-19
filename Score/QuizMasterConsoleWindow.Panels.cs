using System;
using System.Windows;
using System.Windows.Controls;

namespace Score
{
    public partial class QuizMasterConsoleWindow
    {
        private GridLength _savedLeftWidth = new GridLength(280);
        private GridLength _savedRightWidth = new GridLength(340);
        private bool _isLeftCollapsed = false;
        private bool _isRightCollapsed = false;

        public void ToggleLeftPanel()
        {
            if (_isLeftCollapsed)
            {
                LeftOutlineColumn.Width = _savedLeftWidth.Value > 0 ? _savedLeftWidth : new GridLength(280);
                _isLeftCollapsed = false;
                if (ExpandLeftButton != null) ExpandLeftButton.Visibility = Visibility.Collapsed;
                if (CollapseLeftButton != null) CollapseLeftButton.Content = "◀";
                ConsoleStatusText.Text = "Outline panel expanded.";
            }
            else
            {
                if (LeftOutlineColumn.Width.Value > 0)
                    _savedLeftWidth = LeftOutlineColumn.Width;
                LeftOutlineColumn.Width = new GridLength(0);
                _isLeftCollapsed = true;
                if (ExpandLeftButton != null) ExpandLeftButton.Visibility = Visibility.Visible;
                ConsoleStatusText.Text = "Outline panel collapsed. Press Ctrl+[ to restore.";
            }
            UpdateFocusModeButtonLabel();
        }

        public void ToggleRightPanel()
        {
            if (_isRightCollapsed)
            {
                RightTeamsColumn.Width = _savedRightWidth.Value > 0 ? _savedRightWidth : new GridLength(340);
                _isRightCollapsed = false;
                if (ExpandRightButton != null) ExpandRightButton.Visibility = Visibility.Collapsed;
                if (CollapseRightButton != null) CollapseRightButton.Content = "▶";
                ConsoleStatusText.Text = "Teams panel expanded.";
            }
            else
            {
                if (RightTeamsColumn.Width.Value > 0)
                    _savedRightWidth = RightTeamsColumn.Width;
                RightTeamsColumn.Width = new GridLength(0);
                _isRightCollapsed = true;
                if (ExpandRightButton != null) ExpandRightButton.Visibility = Visibility.Visible;
                ConsoleStatusText.Text = "Teams panel collapsed. Press Ctrl+] to restore.";
            }
            UpdateFocusModeButtonLabel();
        }

        public void ToggleFocusMode()
        {
            if (_isLeftCollapsed && _isRightCollapsed)
            {
                // Restore both
                ToggleLeftPanel();
                ToggleRightPanel();
                ConsoleStatusText.Text = "Focus mode exited. All panels restored.";
            }
            else
            {
                // Collapse both
                if (!_isLeftCollapsed) ToggleLeftPanel();
                if (!_isRightCollapsed) ToggleRightPanel();
                ConsoleStatusText.Text = "Focus mode active. Center question expanded. Press Ctrl+F to exit.";
            }
        }

        private void UpdateFocusModeButtonLabel()
        {
            if (FocusModeButton != null)
            {
                FocusModeButton.Content = (_isLeftCollapsed && _isRightCollapsed) ? "EXIT FOCUS" : "FOCUS";
            }
        }

        private void CollapseLeftButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleLeftPanel();
        }

        private void ExpandLeftButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleLeftPanel();
        }

        private void CollapseRightButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleRightPanel();
        }

        private void ExpandRightButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleRightPanel();
        }

        private void FocusModeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleFocusMode();
        }
    }
}
