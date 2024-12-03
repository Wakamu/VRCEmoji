using System.Windows;
using System.Windows.Controls;
using VRCEMoji.EmojiApi;
using VRCEMoji.EmojiGeneration;
using XamlAnimatedGif;

namespace VRCEMoji
{
    public partial class UploadDialog : Window
    {
        public UploadSettings Settings { get; set; }

        private GenerationResult _result;

        public UploadDialog(GenerationResult result)
        {
            InitializeComponent();
            if (result.GenerationType == GenerationType.Sticker)
            {
                fpsSlider.Visibility = Visibility.Hidden;
                fpsLabel.Visibility = Visibility.Hidden;
                fpsValue.Visibility = Visibility.Hidden;
                styleBox.Visibility = Visibility.Hidden;
                stylePreview.Visibility = Visibility.Hidden;
                animationLabel.Visibility = Visibility.Hidden;
                loopLabel.Visibility = Visibility.Hidden;
                loopBox.Visibility = Visibility.Hidden;
            } else
            {
                fpsSlider.Visibility = Visibility.Visible;
                fpsLabel.Visibility = Visibility.Visible;
                fpsValue.Visibility = Visibility.Visible;
                styleBox.Visibility = Visibility.Visible;
                stylePreview.Visibility = Visibility.Visible;
                animationLabel.Visibility = Visibility.Visible;
                loopLabel.Visibility = Visibility.Visible;
                loopBox.Visibility = Visibility.Visible;
            }
            styleBox.ItemsSource = Enum.GetValues(typeof(AnimationStyle)).Cast< AnimationStyle>();
            loopBox.ItemsSource = Enum.GetValues(typeof(LoopStyle)).Cast< LoopStyle>();
            fpsSlider.Value = result.FPS;
            _result = result;
        }

        private void styleBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AnimationBehavior.SetSourceUri(this.stylePreview, new Uri("pack://application:,,,/VRCEMoji;component/Images/" + e.AddedItems[0] +".gif"));
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            styleBox.SelectedIndex = 0;
            loopBox.SelectedIndex = 0;
            SpriteSheetBehaviour.SetSpriteSheet(this.resultBrush, _result.Image, _result.Frames, _result.Columns, _result.Columns, _result.FPS, 128, 128);
        }

        private void upload_Click(object sender, RoutedEventArgs e)
        {
            this.Settings = new UploadSettings { LoopStyle = (LoopStyle)loopBox.SelectedItem, AnimationStyle = (AnimationStyle)styleBox.SelectedItem, FPSOverride = (int)fpsSlider.Value};
            this.DialogResult = true;
        }

        private void fpsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider slider = sender as Slider;
            this.fpsValue.Content = slider.Value;
            SpriteSheetBehaviour.UpdateSpriteSheet(this.resultBrush, (int)slider.Value);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AnimationBehavior.SetSourceUri(this.stylePreview, null);
            SpriteSheetBehaviour.SetSpriteSheet(this.resultBrush, null);
        }

        private void loopBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SpriteSheetBehaviour.UpdateSpriteSheet(this.resultBrush, (int)fpsSlider.Value, (LoopStyle)loopBox.SelectedItem);
        }
    }
}
