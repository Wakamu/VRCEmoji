using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VRCEMoji.EmojiApi;
using VRCEMoji.EmojiGeneration;
using XamlAnimatedGif;

namespace VRCEMoji.Overlays
{
    public partial class UploadOverlay : UserControl
    {
        private TaskCompletionSource<(bool Success, UploadSettings? Settings)>? _tcs;
        private GenerationResult? _result;
        private System.Windows.Threading.DispatcherOperation? _pendingSprite;

        public UploadOverlay()
        {
            InitializeComponent();
            styleBox.ItemsSource = Enum.GetValues(typeof(AnimationStyle)).Cast<AnimationStyle>();
            loopBox.ItemsSource = Enum.GetValues(typeof(LoopStyle)).Cast<LoopStyle>();
        }

        public Task<(bool Success, UploadSettings? Settings)> ShowAsync(GenerationResult result)
        {
            _tcs = new TaskCompletionSource<(bool, UploadSettings?)>();
            _result = result;

            if (result.GenerationType == GenerationType.Sticker)
            {
                fpsSlider.Visibility = Visibility.Collapsed;
                fpsValue.Visibility = Visibility.Collapsed;
                styleBox.Visibility = Visibility.Collapsed;
                stylePreview.Visibility = Visibility.Collapsed;
                animationLabel.Visibility = Visibility.Collapsed;
                loopBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                fpsSlider.Visibility = Visibility.Visible;
                fpsValue.Visibility = Visibility.Visible;
                styleBox.Visibility = Visibility.Visible;
                stylePreview.Visibility = Visibility.Visible;
                animationLabel.Visibility = Visibility.Visible;
                loopBox.Visibility = Visibility.Visible;
            }

            fpsSlider.Value = result.FPS;
            fpsValue.Text = result.FPS.ToString();
            styleBox.SelectedIndex = 0;
            loopBox.SelectedIndex = 0;

            Visibility = Visibility.Visible;

            // Set sprite after making visible so the layout is rendered
            _pendingSprite = Dispatcher.BeginInvoke(new Action(() =>
            {
                SpriteSheetBehaviour.SetSpriteSheet(resultBrush, result.Image, result.Frames,
                    result.Columns, result.Columns, result.FPS, 80, 80);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            return _tcs.Task;
        }

        private void Upload_Click(object sender, RoutedEventArgs e)
        {
            var settings = new UploadSettings
            {
                LoopStyle = (LoopStyle)loopBox.SelectedItem,
                AnimationStyle = (AnimationStyle)styleBox.SelectedItem,
                FPSOverride = (int)fpsSlider.Value
            };
            CleanUp();
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult((true, settings));
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { CleanUp(); Visibility = Visibility.Collapsed; _tcs?.TrySetResult((false, null)); }
        private void Backdrop_Click(object sender, MouseButtonEventArgs e) { CleanUp(); Visibility = Visibility.Collapsed; _tcs?.TrySetResult((false, null)); }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CleanUp();
                Visibility = Visibility.Collapsed;
                _tcs?.TrySetResult((false, null));
            }
        }

        private void CleanUp()
        {
            _pendingSprite?.Abort();
            _pendingSprite = null;
            AnimationBehavior.SetSourceUri(stylePreview, null);
            SpriteSheetBehaviour.SetSpriteSheet(resultBrush, null);
        }

        private void styleBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
                AnimationBehavior.SetSourceUri(stylePreview, new Uri("pack://application:,,,/VRCEMoji;component/Images/" + e.AddedItems[0] + ".gif"));
        }

        private void fpsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (fpsValue != null)
            {
                fpsValue.Text = ((int)e.NewValue).ToString();
                SpriteSheetBehaviour.UpdateSpriteSheet(resultBrush, (int)e.NewValue);
            }
        }

        private void loopBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && fpsSlider != null)
                SpriteSheetBehaviour.UpdateSpriteSheet(resultBrush, (int)fpsSlider.Value, (LoopStyle)e.AddedItems[0]);
        }
    }
}
