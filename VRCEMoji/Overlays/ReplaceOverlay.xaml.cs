using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VRCEMoji.EmojiApi;
using VRCEmoji.EmojiApi;

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

        public Task<(bool Success, string SelectedId)> ShowAsync(List<EmojiFile> files)
        {
            _tcs = new TaskCompletionSource<(bool, string)>();
            _selectedId = "";
            replaceBtn.IsEnabled = false;
            subtitleText.Text = $"Select an emoji to replace ({files.Count}/18 slots used)";

            var viewModels = new List<EmojiViewModel>();
            foreach (var file in files)
            {
                var vm = new EmojiViewModel { Id = file.Id };
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.UriSource = new Uri("https://api.vrchat.cloud/api/1/file/" + file.Id + "/1/file");
                bi.EndInit();

                if (file.Frames > 0)
                {
                    bi.DownloadCompleted += (sender, e) =>
                    {
                        var bmp = (BitmapImage)sender!;
                        int cropSize = file.Frames > 4 ? file.Frames > 16 ? 128 : 256 : 512;
                        if (cropSize <= bmp.PixelWidth && cropSize <= bmp.PixelHeight)
                            vm.Thumbnail = new CroppedBitmap(bmp, new Int32Rect(0, 0, cropSize, cropSize));
                        else
                            vm.Thumbnail = bmp;
                    };
                }
                vm.Thumbnail = bi;
                viewModels.Add(vm);
            }

            emojiGrid.ItemsSource = viewModels;
            Visibility = Visibility.Visible;
            return _tcs.Task;
        }

        private void Emoji_Click(object sender, MouseButtonEventArgs e)
        {
            var border = (Border)sender;
            var vm = (EmojiViewModel)border.DataContext;
            _selectedId = vm.Id;

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
