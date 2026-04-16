using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VRCEMoji.Overlays
{
    public partial class InputOverlay : UserControl
    {
        private TaskCompletionSource<(bool Success, string Answer)>? _tcs;

        public InputOverlay() { InitializeComponent(); }

        public Task<(bool Success, string Answer)> ShowAsync(string question)
        {
            _tcs = new TaskCompletionSource<(bool, string)>();
            questionText.Text = question;
            answerBox.Text = "";
            Visibility = Visibility.Visible;
            answerBox.Focus();
            return _tcs.Task;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult((true, answerBox.Text));
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult((false, ""));
        }

        private void Backdrop_Click(object sender, MouseButtonEventArgs e)
        {
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult((false, ""));
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Visibility = Visibility.Collapsed;
                _tcs?.TrySetResult((false, ""));
            }
        }
    }
}
