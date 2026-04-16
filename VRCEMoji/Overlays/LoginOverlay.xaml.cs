using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VRCEMoji.Overlays
{
    public partial class LoginOverlay : UserControl
    {
        private TaskCompletionSource<(bool Success, string Login, string Password)>? _tcs;

        public LoginOverlay() { InitializeComponent(); }

        public Task<(bool Success, string Login, string Password)> ShowAsync(string? error = null)
        {
            _tcs = new TaskCompletionSource<(bool, string, string)>();
            if (error != null)
            {
                errorText.Text = error;
                errorBorder.Visibility = Visibility.Visible;
                passwordBox.Password = "";
            }
            else
            {
                loginBox.Text = "";
                passwordBox.Password = "";
                errorBorder.Visibility = Visibility.Collapsed;
                errorText.Text = "";
            }
            Visibility = Visibility.Visible;
            loginBox.Focus();
            return _tcs.Task;
        }

        private void SignIn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(loginBox.Text) || string.IsNullOrWhiteSpace(passwordBox.Password))
            {
                errorText.Text = "Please enter both username and password.";
                errorBorder.Visibility = Visibility.Visible;
                return;
            }
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult((true, loginBox.Text, passwordBox.Password));
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult((false, "", ""));
        }

        private void Backdrop_Click(object sender, MouseButtonEventArgs e)
        {
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult((false, "", ""));
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Visibility = Visibility.Collapsed;
                _tcs?.TrySetResult((false, "", ""));
            }
        }
    }
}
