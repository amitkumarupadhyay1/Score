using System;
using System.Diagnostics;
using System.Windows;

namespace Score
{
    public partial class QuizBuzzerDialog : Window
    {
        private readonly QuizBuzzerServer _server;

        public QuizBuzzerDialog(QuizBuzzerServer server)
        {
            InitializeComponent();
            _server = server;
            Loaded += QuizBuzzerDialog_Loaded;
        }

        private void QuizBuzzerDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (_server == null) return;

            string url = _server.LocalUrl;
            ServerUrlTextBox.Text = url;

            try
            {
                var bitmap = QuizBuzzerServer.GenerateQrBitmap(url, 7, 4);
                QrCodeImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not generate QR code in dialog.", ex);
            }
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ServerUrlTextBox.Text);
                DialogStatusText.Text = "URL copied to clipboard!";
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to copy URL to clipboard.", ex);
            }
        }

        private void OpenBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ServerUrlTextBox.Text,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to open browser.", ex);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
