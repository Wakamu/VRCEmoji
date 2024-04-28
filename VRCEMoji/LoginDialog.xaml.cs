using System.Windows;

namespace VRCEMoji
{
    public partial class LoginDialog : Window
    {
        public LoginDialog()
        {
            InitializeComponent();
        }

        public string Login
        {
            get { return loginBox.Text; }
        }

        public string Password
        {
            get { return passwordBox.Password; }
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            loginBox.SelectAll();
            loginBox.Focus();
        }
    }
}
