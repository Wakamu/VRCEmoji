using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace VRCEMoji.Overlays
{
    public partial class StatusOverlay : UserControl
    {
        private TaskCompletionSource<bool>? _tcs;
        private bool _isDismissible;

        public StatusOverlay() { InitializeComponent(); }

        public void ShowLoading(string message)
        {
            _tcs = null;
            _isDismissible = false;

            spinnerContainer.Visibility = Visibility.Visible;
            successIcon.Visibility = Visibility.Collapsed;
            errorIcon.Visibility = Visibility.Collapsed;
            okButton.Visibility = Visibility.Collapsed;
            messageText.Text = message;

            Visibility = Visibility.Visible;

            var storyboard = (Storyboard)Resources["SpinAnimation"];
            storyboard.Begin();
        }

        public Task ShowSuccess(string message) => ShowResult(true, message);

        public Task ShowError(string message) => ShowResult(false, message);

        private Task ShowResult(bool isSuccess, string message)
        {
            _tcs = new TaskCompletionSource<bool>();
            _isDismissible = true;

            StopSpinner();
            spinnerContainer.Visibility = Visibility.Collapsed;
            successIcon.Visibility = isSuccess ? Visibility.Visible : Visibility.Collapsed;
            errorIcon.Visibility = isSuccess ? Visibility.Collapsed : Visibility.Visible;
            okButton.Visibility = Visibility.Visible;
            messageText.Text = message;

            Visibility = Visibility.Visible;
            okButton.Focus();

            return _tcs.Task;
        }

        public void Hide()
        {
            StopSpinner();
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult(true);
        }

        private void StopSpinner()
        {
            var storyboard = (Storyboard)Resources["SpinAnimation"];
            storyboard.Stop();
        }

        private void Dismiss()
        {
            if (!_isDismissible) return;
            StopSpinner();
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult(true);
        }

        private void Ok_Click(object sender, RoutedEventArgs e) => Dismiss();

        private void Backdrop_Click(object sender, MouseButtonEventArgs e) => Dismiss();

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Dismiss();
                e.Handled = true;
            }
        }
    }
}
