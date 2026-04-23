using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VRCEMoji.EmojiApi;
using VRCEmoji.EmojiApi;
using XamlAnimatedGif;
using System.Windows.Shapes;

namespace VRCEMoji.Overlays
{
    public class EmojiViewModel
    {
        public string Id { get; set; } = "";
        public ImageSource? Thumbnail { get; set; }
    }

    public partial class ReplaceOverlay : UserControl
    {
        private TaskCompletionSource<(bool Success, string SelectedId)>? _tcs;
        private string _selectedId = "";
        private Border? _previousSelection;

        public ReplaceOverlay() { InitializeComponent(); }

        public Task<(bool Success, string SelectedId)> ShowAsync(List<ManagedFile> files)
        {
            _tcs = new TaskCompletionSource<(bool, string)>();
            _selectedId = "";
            replaceBtn.IsEnabled = false;
            subtitleText.Text = $"Select an emoji to replace ({files.Count}/18 slots used)";

            var viewModels = new List<FileViewModel>();
            foreach (var file in files)
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.UriSource = new Uri(file.ImageUrl);
                bi.EndInit();
                int frames = file.DetectedFrames;
                bool animate = file.IsAnimated && frames > 1;
                var fm = new FileViewModel
                {
                    TypeLabel = file.Frames == 0 ? "Sticker" : "Animated",
                    File = file,
                    Thumbnail = bi,
                    IsAnimated = file.IsAnimated,
                    Frames = animate ? frames : 0,
                    Columns = animate ? (frames <= 4 ? 2 : frames <= 16 ? 4 : 8) : 0,
                    FPS = animate ? file.DetectedFPS : 0,
                };

                viewModels.Add(fm);
            }

            emojiGrid.ItemsSource = viewModels;
            Visibility = Visibility.Visible;
            return _tcs.Task;
        }

        private void Thumb_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Rectangle rect ||
                rect.DataContext is not FileViewModel vm) return;

            // Brushes declared in a DataTemplate are frozen by WPF, so construct
            // a fresh mutable ImageBrush per Rectangle instance.
            var brush = new ImageBrush
            {
                Stretch = Stretch.Uniform,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };
            rect.Fill = brush;

            if (vm.IsAnimated && vm.Thumbnail != null)
            {
                SpriteSheetBehaviour.SetSpriteSheetFromSource(
                    brush, vm.Thumbnail, vm.Frames, vm.Columns, vm.Columns, vm.FPS, 56, 56);
            }
            else
            {
                brush.ImageSource = vm.Thumbnail;
            }
        }

        private void Thumb_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Rectangle rect || rect.Fill is not ImageBrush brush) return;
            SpriteSheetBehaviour.SetSpriteSheetFromSource(brush, null);
        }

        private void Emoji_Click(object sender, MouseButtonEventArgs e)
        {
            var border = (Border)sender;
            var vm = (FileViewModel)border.DataContext;
            _selectedId = vm.File.Id;

            if (_previousSelection != null)
            {
                _previousSelection.BorderBrush = (SolidColorBrush)FindResource("BorderInputBrush");
                _previousSelection.BorderThickness = new Thickness(1);
            }
            border.BorderBrush = (SolidColorBrush)FindResource("AccentBrush");
            border.BorderThickness = new Thickness(2);
            _previousSelection = border;
            replaceBtn.IsEnabled = true;
        }

        private void Replace_Click(object sender, RoutedEventArgs e) { Visibility = Visibility.Collapsed; _tcs?.TrySetResult((true, _selectedId)); }
        private void Cancel_Click(object sender, RoutedEventArgs e) { Visibility = Visibility.Collapsed; _tcs?.TrySetResult((false, "")); }
        private void Backdrop_Click(object sender, MouseButtonEventArgs e) { Visibility = Visibility.Collapsed; _tcs?.TrySetResult((false, "")); }

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
