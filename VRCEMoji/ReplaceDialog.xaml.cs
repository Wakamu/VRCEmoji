using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VRCEmoji.EmojiApi;

namespace VRCEMoji
{
    /// <summary>
    /// Logique d'interaction pour ReplaceDialog.xaml
    /// </summary>
    public partial class ReplaceDialog : Window
    {
        public string SelectedId { get; set; } = string.Empty;
        private List<EmojiFile> files;

        public ReplaceDialog(List<EmojiFile> files)
        {
            InitializeComponent();
            this.files = files;
            Image[] images = { Emoji1, Emoji2, Emoji3, Emoji4, Emoji5, Emoji6, Emoji7, Emoji8, Emoji9 };
            int i = 0;
            foreach (EmojiFile file in files)
            {
                var currentImage = images[i];
                currentImage.Source = null;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.UriSource = new Uri("https://api.vrchat.cloud/api/1/file/" + file.Id + "/1/file");
                bi.EndInit();
                if (file.Frames > 0)
                {
                    bi.DownloadCompleted += new EventHandler((sender, e) => Emoji_DownloadCompleted(sender, e, currentImage, file));
                }
                
                currentImage.Source = bi;
                currentImage.Tag = file.Id;
                i++;
            }
        }

        private void Emoji_DownloadCompleted(object? sender, EventArgs e, Image image, EmojiFile file)
        {
            BitmapImage bi = (BitmapImage)sender;
            int width = bi.PixelWidth; int height = bi.PixelHeight;
            int cropSize = file.Frames > 4 ? file.Frames > 16 ? 128 : 256 : 512;
            CroppedBitmap cropped = new CroppedBitmap(bi, new Int32Rect(0, 0, cropSize, cropSize));
            image.Source = cropped;
        }

        private void Emoji_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Image image = sender as Image;
            SelectedId = (string)image.Tag;
            this.DialogResult = true;
        }
    }
}
