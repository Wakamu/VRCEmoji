using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VRCEMoji.EmojiApi;

namespace VRCEMoji
{
    public class FileViewModel
    {
        public ImageSource? Thumbnail { get; set; }
        public string TypeLabel { get; set; } = "";
        public ManagedFile File { get; set; } = null!;
        public bool IsAnimated { get; set; }
        public int Frames { get; set; }
        public int Columns { get; set; }
        public int FPS { get; set; }
    }

    public partial class ManageView : UserControl
    {
        private List<ManagedFile>? _allFiles;
        private EmojiApi.EmojiApi? _fileApi;
        private string? _lastAppliedFilter;

        public event Action<ManagedFile>? FileClicked;

        public ManageView()
        {
            InitializeComponent();
            filterBox.Items.Add("All");
            filterBox.Items.Add("Emoji");
            filterBox.Items.Add("Sticker");
            filterBox.SelectedIndex = 0;
        }

        public async Task LoadFilesAsync(EmojiApi.EmojiApi fileApi)
        {
            _fileApi = fileApi;
            _lastAppliedFilter = null;
            emptyText.Visibility = Visibility.Collapsed;
            gridScroller.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Visible;

            try
            {
                var emojiTask = Task.Run(() => fileApi.GetFiles("emoji", 100, 0));
                var animatedTask = Task.Run(() => fileApi.GetFiles("emojianimated", 100, 0));
                var stickerTask = Task.Run(() => fileApi.GetFiles("sticker", 100, 0));
                await Task.WhenAll(emojiTask, animatedTask, stickerTask);

                _allFiles = [.. emojiTask.Result, .. animatedTask.Result, .. stickerTask.Result];

                ApplyFilter();
            }
            catch (Exception ex)
            {
                loadingPanel.Visibility = Visibility.Collapsed;
                emptyText.Text = "Failed to load files: " + ex.Message;
                emptyText.Visibility = Visibility.Visible;
            }
        }

        public void ShowNotLoggedIn()
        {
            // Drop the cached API client so the Refresh button can't silently
            // reach VRChat with a pre-logout Configuration.
            _fileApi = null;
            _allFiles = null;
            _lastAppliedFilter = null;
            fileGrid.ItemsSource = null;
            gridScroller.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Collapsed;
            emptyText.Text = "Log in to manage your emojis and stickers";
            emptyText.Visibility = Visibility.Visible;
        }

        private void ApplyFilter()
        {
            if (_allFiles == null) return;

            string filter = (string)filterBox.SelectedItem;
            if (filter == _lastAppliedFilter && fileGrid.ItemsSource != null) return;
            _lastAppliedFilter = filter;

            loadingPanel.Visibility = Visibility.Collapsed;

            var filtered = filter switch
            {
                "Emoji" => _allFiles.Where(f => f.IsEmoji).ToList(),
                "Sticker" => _allFiles.Where(f => f.IsSticker).ToList(),
                _ => _allFiles.ToList()
            };

            if (filtered.Count == 0)
            {
                fileGrid.ItemsSource = null;
                gridScroller.Visibility = Visibility.Collapsed;
                emptyText.Text = "No " + filter.ToLower() + " files found";
                emptyText.Visibility = Visibility.Visible;
                return;
            }

            emptyText.Visibility = Visibility.Collapsed;
            gridScroller.Visibility = Visibility.Visible;

            var viewModels = new List<FileViewModel>();
            foreach (var file in filtered)
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.UriSource = new Uri(file.ImageUrl);
                bi.EndInit();

                int frames = file.DetectedFrames;
                bool animate = file.IsAnimated && frames > 1;

                viewModels.Add(new FileViewModel
                {
                    TypeLabel = file.IsSticker ? "Sticker" : (file.IsAnimated ? "Animated" : "Emoji"),
                    File = file,
                    Thumbnail = bi,
                    IsAnimated = animate,
                    Frames = animate ? frames : 0,
                    Columns = animate ? (frames <= 4 ? 2 : frames <= 16 ? 4 : 8) : 0,
                    FPS = animate ? file.DetectedFPS : 0,
                });
            }

            fileGrid.ItemsSource = viewModels;
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

        private void filterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allFiles != null)
            {
                ApplyFilter();
            }
        }

        private void File_Click(object sender, MouseButtonEventArgs e)
        {
            var border = (Border)sender;
            var vm = (FileViewModel)border.DataContext;
            FileClicked?.Invoke(vm.File);
        }

        private async void refreshBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_fileApi != null)
            {
                await LoadFilesAsync(_fileApi);
            }
        }
    }
}
