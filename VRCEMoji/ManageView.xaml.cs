using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VRCEMoji.EmojiApi;

namespace VRCEMoji
{
    public class FileViewModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public ImageSource? Thumbnail { get; set; }
        public string TypeLabel { get; set; } = "";
        public ManagedFile File { get; set; } = null!;
    }

    public partial class ManageView : UserControl
    {
        private List<ManagedFile>? _allFiles;
        private EmojiApi.EmojiApi? _fileApi;

        public event Action<ManagedFile>? FileClicked;

        public ManageView()
        {
            InitializeComponent();
            filterBox.Items.Add("All");
            filterBox.Items.Add("Emoji");
            filterBox.Items.Add("Sticker");
            filterBox.SelectedIndex = 0;
        }

        public void SetApi(EmojiApi.EmojiApi fileApi)
        {
            _fileApi = fileApi;
        }

        public async Task LoadFilesAsync(EmojiApi.EmojiApi fileApi)
        {
            _fileApi = fileApi;
            emptyText.Visibility = Visibility.Collapsed;
            gridScroller.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Visible;

            try
            {
                _allFiles = await Task.Run(() =>
                {
                    var emojis = fileApi.GetFiles("emoji", 100, 0);
                    var animatedEmojis = fileApi.GetFiles("emojianimated", 100, 0);
                    var stickers = fileApi.GetFiles("sticker", 100, 0);
                    var all = new List<ManagedFile>();
                    all.AddRange(emojis);
                    all.AddRange(animatedEmojis);
                    all.AddRange(stickers);
                    return all;
                });

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
            _allFiles = null;
            fileGrid.ItemsSource = null;
            gridScroller.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Collapsed;
            emptyText.Text = "Log in to manage your emojis and stickers";
            emptyText.Visibility = Visibility.Visible;
        }

        private void ApplyFilter()
        {
            if (_allFiles == null) return;

            loadingPanel.Visibility = Visibility.Collapsed;
            string filter = (string)filterBox.SelectedItem;

            var filtered = filter switch
            {
                "Emoji" => _allFiles.Where(f => f.IsEmoji).ToList(),
                "Sticker" => _allFiles.Where(f => f.IsSticker).ToList(),
                _ => _allFiles
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
                var vm = new FileViewModel
                {
                    Id = file.Id,
                    Name = file.Name,
                    TypeLabel = file.IsSticker ? "Sticker" : (file.IsAnimated ? "Animated" : "Emoji"),
                    File = file
                };

                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.UriSource = new Uri(file.ImageUrl);
                bi.EndInit();

                if (file.Frames > 0 && file.IsAnimated)
                {
                    bi.DownloadCompleted += (sender, e) =>
                    {
                        var bmp = (BitmapImage)sender!;
                        int cropSize = file.Frames > 16 ? 128 : file.Frames > 4 ? 256 : 512;
                        if (cropSize <= bmp.PixelWidth && cropSize <= bmp.PixelHeight)
                            vm.Thumbnail = new CroppedBitmap(bmp, new System.Windows.Int32Rect(0, 0, cropSize, cropSize));
                        else
                            vm.Thumbnail = bmp;
                    };
                }

                vm.Thumbnail = bi;
                viewModels.Add(vm);
            }

            fileGrid.ItemsSource = viewModels;
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
