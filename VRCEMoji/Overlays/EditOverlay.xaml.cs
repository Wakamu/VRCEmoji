using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VRCEMoji.EmojiApi;

namespace VRCEMoji.Overlays
{
    public enum EditAction
    {
        Cancel,
        Save,
        Delete,
        ReplaceImage
    }

    public class EditResult
    {
        public EditAction Action { get; set; } = EditAction.Cancel;
        public ManagedFile? File { get; set; }
        public string? UpdatedName { get; set; }
        public AnimationStyle? UpdatedAnimationStyle { get; set; }
        public LoopStyle? UpdatedLoopStyle { get; set; }
        public int? UpdatedFPS { get; set; }
    }

    public partial class EditOverlay : UserControl
    {
        private TaskCompletionSource<EditResult>? _tcs;
        private ManagedFile? _currentFile;
        private CancellationTokenSource? _loadCts;

        public EditOverlay()
        {
            InitializeComponent();
            animStyleBox.ItemsSource = Enum.GetValues(typeof(AnimationStyle)).Cast<AnimationStyle>();
            loopStyleBox.ItemsSource = Enum.GetValues(typeof(LoopStyle)).Cast<LoopStyle>();
        }

        public Task<EditResult> ShowAsync(ManagedFile file)
        {
            _tcs = new TaskCompletionSource<EditResult>();
            _currentFile = file;

            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            titleText.Text = file.IsSticker ? "Edit Sticker" : "Edit Emoji";
            nameBox.Text = file.Name;

            SpriteSheetBehaviour.SetSpriteSheetFromSource(spriteBrush, null);

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.UriSource = new Uri(file.ImageUrl);
            bi.EndInit();

            if (file.IsAnimated)
            {
                staticPreviewBorder.Visibility = Visibility.Collapsed;
                animatedPreviewBorder.Visibility = Visibility.Visible;

                int knownFrames = file.DetectedFrames;
                int fps = file.DetectedFPS;

                bi.DownloadCompleted += (sender, e) =>
                {
                    if (token.IsCancellationRequested) return;
                    var bmp = (BitmapImage)sender!;

                    int frames = knownFrames;
                    if (frames <= 0 && bmp.PixelWidth > 0)
                    {
                        // Fallback: detect grid from image. VRChat spritesheets are 1024x1024
                        // with 2x2 (512px cells), 4x4 (256px cells), or 8x8 (128px cells).
                        frames = DetectFrameCount(bmp);
                    }

                    if (frames > 1)
                    {
                        int columns = frames <= 4 ? 2 : frames <= 16 ? 4 : 8;
                        SpriteSheetBehaviour.SetSpriteSheetFromSource(spriteBrush, bmp, frames, columns, columns, fps, 80, 80);
                    }
                };
                spriteBrush.ImageSource = bi;
            }
            else
            {
                staticPreviewBorder.Visibility = Visibility.Visible;
                animatedPreviewBorder.Visibility = Visibility.Collapsed;
                previewImage.Source = bi;
            }

            bool isEmoji = file.IsEmoji;
            bool isAnimated = file.IsAnimated;

            animStyleLabel.Visibility = isEmoji ? Visibility.Visible : Visibility.Collapsed;
            animStyleBox.Visibility = isEmoji ? Visibility.Visible : Visibility.Collapsed;
            loopStyleLabel.Visibility = isEmoji ? Visibility.Visible : Visibility.Collapsed;
            loopStyleBox.Visibility = isEmoji ? Visibility.Visible : Visibility.Collapsed;
            fpsLabel.Visibility = isAnimated ? Visibility.Visible : Visibility.Collapsed;
            fpsRow.Visibility = isAnimated ? Visibility.Visible : Visibility.Collapsed;

            if (isEmoji)
            {
                animStyleBox.SelectedItem = EnumHelper.FindByMemberValue<AnimationStyle>(file.AnimationStyle)
                    ?? Enum.GetValues(typeof(AnimationStyle)).Cast<AnimationStyle>().First();

                loopStyleBox.SelectedItem = EnumHelper.FindByMemberValue<LoopStyle>(file.LoopStyle)
                    ?? Enum.GetValues(typeof(LoopStyle)).Cast<LoopStyle>().First();
            }

            if (isAnimated)
            {
                fpsSlider.Value = file.DetectedFPS;
                fpsValue.Text = ((int)fpsSlider.Value).ToString();
            }

            Visibility = Visibility.Visible;
            return _tcs.Task;
        }

        private EditResult BuildResult(EditAction action)
        {
            var result = new EditResult
            {
                Action = action,
                File = _currentFile
            };

            if (action == EditAction.Save && _currentFile != null)
            {
                result.UpdatedName = nameBox.Text;
                if (_currentFile.IsEmoji)
                {
                    result.UpdatedAnimationStyle = (AnimationStyle?)animStyleBox.SelectedItem;
                    result.UpdatedLoopStyle = (LoopStyle?)loopStyleBox.SelectedItem;
                }
                if (_currentFile.IsAnimated)
                {
                    result.UpdatedFPS = (int)fpsSlider.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Detects frame count from a 1024x1024 spritesheet by checking pixel alpha
        /// at cell boundaries. VRChat uses 2x2 (512px), 4x4 (256px), or 8x8 (128px) grids.
        /// </summary>
        private static int DetectFrameCount(BitmapImage bmp)
        {
            if (bmp.PixelWidth < 256 || bmp.PixelHeight < 256)
                return 4; // safe default

            // Read a single pixel at a given position to check if it has content
            bool HasContent(int x, int y)
            {
                var cb = new CroppedBitmap(bmp, new Int32Rect(x, y, 1, 1));
                byte[] pixel = new byte[4];
                cb.CopyPixels(pixel, 4, 0);
                return pixel[3] > 10; // alpha > 10 means non-empty
            }

            // Check the top-left pixel of the 2nd cell in each possible grid.
            // If content exists at (256, 0), there are more than 4 frames (not just 2x2).
            // If content exists at (128, 0), there are more than 16 frames (8x8 grid).
            bool has4x4Content = HasContent(256, 0);
            if (!has4x4Content) return 4;    // only 2x2 cells have content

            bool has8x8Content = HasContent(128, 0);
            if (!has8x8Content) return 16;   // 4x4 grid

            return 64;                       // 8x8 grid
        }

        private void Dismiss(EditAction action)
        {
            _loadCts?.Cancel();
            SpriteSheetBehaviour.SetSpriteSheetFromSource(spriteBrush, null);
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult(BuildResult(action));
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will re-upload the " + (_currentFile?.IsSticker == true ? "sticker" : "emoji") + " with updated settings. Continue?",
                "Confirm Save",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
                Dismiss(EditAction.Save);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Delete this " + (_currentFile?.IsSticker == true ? "sticker" : "emoji") + "? This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
                Dismiss(EditAction.Delete);
        }

        private void ReplaceImage_Click(object sender, RoutedEventArgs e) => Dismiss(EditAction.ReplaceImage);

        private void Cancel_Click(object sender, RoutedEventArgs e) => Dismiss(EditAction.Cancel);

        private void Backdrop_Click(object sender, MouseButtonEventArgs e) => Dismiss(EditAction.Cancel);

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Dismiss(EditAction.Cancel);
                e.Handled = true;
            }
        }

        private void fpsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (fpsValue != null)
            {
                fpsValue.Text = ((int)e.NewValue).ToString();
            }
        }
    }
}
