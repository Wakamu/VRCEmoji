using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            titleText.Text = file.IsSticker ? "Edit Sticker" : "Edit Emoji";
            nameBox.Text = file.Name;

            // Stop any existing animation
            SpriteSheetBehaviour.SetSpriteSheetFromSource(spriteBrush, null);

            // Load image
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.UriSource = new Uri(file.ImageUrl);
            bi.EndInit();

            if (file.IsAnimated && file.Frames > 0)
            {
                // Show animated preview, hide static
                staticPreviewBorder.Visibility = Visibility.Collapsed;
                animatedPreviewBorder.Visibility = Visibility.Visible;

                int frames = file.Frames;
                int columns = (int)Math.Ceiling(Math.Sqrt(frames));
                int fps = file.FramesOverTime > 0 ? file.FramesOverTime : 8;

                bi.DownloadCompleted += (sender, e) =>
                {
                    var bmp = (BitmapImage)sender!;
                    SpriteSheetBehaviour.SetSpriteSheetFromSource(spriteBrush, bmp, frames, columns, columns, fps, 80, 80);
                };
                spriteBrush.ImageSource = bi;
            }
            else
            {
                // Show static preview, hide animated
                staticPreviewBorder.Visibility = Visibility.Visible;
                animatedPreviewBorder.Visibility = Visibility.Collapsed;
                previewImage.Source = bi;
            }

            // Show/hide emoji-specific fields
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
                // Try to match current animation style
                if (file.AnimationStyle != null)
                {
                    var matchedStyle = Enum.GetValues(typeof(AnimationStyle)).Cast<AnimationStyle>()
                        .FirstOrDefault(s =>
                        {
                            var memberInfo = typeof(AnimationStyle).GetMember(s.ToString()).FirstOrDefault();
                            var attr = memberInfo?.GetCustomAttributes(typeof(EnumMemberAttribute), false).FirstOrDefault() as EnumMemberAttribute;
                            return attr?.Value == file.AnimationStyle;
                        });
                    animStyleBox.SelectedItem = matchedStyle;
                }
                else
                {
                    animStyleBox.SelectedIndex = 0;
                }

                // Try to match current loop style
                if (file.LoopStyle != null)
                {
                    var matchedLoop = Enum.GetValues(typeof(LoopStyle)).Cast<LoopStyle>()
                        .FirstOrDefault(s =>
                        {
                            var memberInfo = typeof(LoopStyle).GetMember(s.ToString()).FirstOrDefault();
                            var attr = memberInfo?.GetCustomAttributes(typeof(EnumMemberAttribute), false).FirstOrDefault() as EnumMemberAttribute;
                            return attr?.Value == file.LoopStyle;
                        });
                    loopStyleBox.SelectedItem = matchedLoop;
                }
                else
                {
                    loopStyleBox.SelectedIndex = 0;
                }
            }

            if (isAnimated)
            {
                fpsSlider.Value = file.FramesOverTime > 0 ? file.FramesOverTime : 8;
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

        private void CleanUp()
        {
            SpriteSheetBehaviour.SetSpriteSheetFromSource(spriteBrush, null);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will re-upload the " + (_currentFile?.IsSticker == true ? "sticker" : "emoji") + " with updated settings. Continue?",
                "Confirm Save",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                CleanUp();
                Visibility = Visibility.Collapsed;
                _tcs?.TrySetResult(BuildResult(EditAction.Save));
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Delete this " + (_currentFile?.IsSticker == true ? "sticker" : "emoji") + "? This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                CleanUp();
                Visibility = Visibility.Collapsed;
                _tcs?.TrySetResult(BuildResult(EditAction.Delete));
            }
        }

        private void ReplaceImage_Click(object sender, RoutedEventArgs e)
        {
            CleanUp();
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult(BuildResult(EditAction.ReplaceImage));
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CleanUp();
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult(BuildResult(EditAction.Cancel));
        }

        private void Backdrop_Click(object sender, MouseButtonEventArgs e)
        {
            CleanUp();
            Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult(BuildResult(EditAction.Cancel));
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CleanUp();
                Visibility = Visibility.Collapsed;
                _tcs?.TrySetResult(BuildResult(EditAction.Cancel));
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
