using System.Windows;
using System.Windows.Controls;
using VRCEMoji.EmojiApi;
using XamlAnimatedGif;

namespace VRCEMoji
{
    public partial class UploadDialog : Window
    {
        public UploadSettings Settings { get; set; }

        public UploadDialog()
        {
            InitializeComponent();
            styleBox.ItemsSource = Enum.GetValues(typeof(AnimationStyle)).Cast< AnimationStyle>();
            loopBox.ItemsSource = Enum.GetValues(typeof(LoopStyle)).Cast< LoopStyle>();
        }

        private void styleBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AnimationBehavior.SetSourceUri(this.stylePreview, new Uri("pack://application:,,,/VRCEMoji;component/Images/" + e.AddedItems[0] +".gif"));
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            styleBox.SelectedIndex = 0;
            loopBox.SelectedIndex = 0;
        }

        private void upload_Click(object sender, RoutedEventArgs e)
        {
            this.Settings = new UploadSettings { LoopStyle = (LoopStyle)loopBox.SelectedItem, AnimationStyle = (AnimationStyle)styleBox.SelectedItem };
            this.DialogResult = true;
        }
    }
}
