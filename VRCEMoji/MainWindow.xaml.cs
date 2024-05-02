using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XamlAnimatedGif;
using System.Windows.Media;
using VRCEMoji.EmojiApi;
using VRCEMoji.EmojiGeneration;

namespace VRCEMoji
{
    public partial class MainWindow : Window
    {
        private GenerationSettings? generationSettings;
        private System.Windows.Point startPoint;
        private System.Windows.Shapes.Rectangle? rect;
        private GenerationResult? generationResult;
        private bool chromaPicker;
        private Rgba32 chromaColor;
        public string? loadedName;
        private static MainWindow? _instance;

        public MainWindow()
        {
            InitializeComponent();
            AnimationBehavior.SetCacheFramesInMemory(this.originalGif, true);
            StoredConfig? authConfig = Authentication.Instance.StoredConfig;
            if (authConfig != null )
            {
                this.loggedLabel.Content = authConfig.DisplayName;
                this.logoff.Visibility = Visibility.Visible;
            }
            chromaTypeBox.ItemsSource = Enum.GetValues(typeof(ChromaType)).Cast<ChromaType>();
            _instance = this;
        }

        public static MainWindow? Instance { get { return _instance; } }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.chromaPicker && this.generationSettings != null)
            {
                System.Windows.Point p = e.GetPosition(canvas);
                int frameIndex = AnimationBehavior.GetAnimator(originalGif).CurrentFrameIndex;
                var currentFrame = this.generationSettings.Image.Frames[frameIndex];
                double cropWRatio = (double)256 / currentFrame.Width;
                double cropHRatio = (double)256 / currentFrame.Height;
                var rgbColor = currentFrame[Math.Min((int)(p.X / cropWRatio), currentFrame.Width), Math.Min((int)(p.Y / cropHRatio), currentFrame.Height)];
                chromaColor = rgbColor;
                this.chromaPicker = false;
                this.chromaButton.Background = new SolidColorBrush(new System.Windows.Media.Color
                {
                    A = rgbColor.A,
                    B = rgbColor.B,
                    R = rgbColor.R,
                    G = rgbColor.G
                });
                Mouse.OverrideCursor = null;
                this.chromaButton.IsEnabled = true;
                this.open.IsEnabled = true;
                this.generate.IsEnabled = true;
                this.save.IsEnabled = true;
                this.upload.IsEnabled = true;
                this.chromaBox.IsEnabled = true;
                this.logoff.IsEnabled = true;
                AnimationBehavior.GetAnimator(originalGif).Play();
            }
            else if (this.cropBox.IsChecked == true)
            {
                rect = null;
                canvas.Children.Clear();
                startPoint = e.GetPosition(canvas);

                rect = new System.Windows.Shapes.Rectangle
                {
                    Stroke = Brushes.LightBlue,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(rect, startPoint.X);
                Canvas.SetTop(rect, startPoint.Y);
                canvas.Children.Add(rect);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (cropBox.IsChecked == true)
            {
                if (e.LeftButton == MouseButtonState.Released || rect == null)
                    return;

                var pos = e.GetPosition(canvas);

                var x = Math.Min(pos.X, startPoint.X);
                var y = Math.Min(pos.Y, startPoint.Y);

                var w = Math.Max(pos.X, startPoint.X) - x;
                var h = Math.Max(pos.Y, startPoint.Y) - y;

                rect.Width = w;
                rect.Height = h;

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
            }
        }

        private void open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                FileName = "Image",
                DefaultExt = ".gif",
                Filter = "Gif (.gif)|*.gif"
            };
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                this.generationSettings?.Dispose();   
                string filename = dialog.FileName;
                loadedName = System.IO.Path.GetFileNameWithoutExtension(filename);
                AnimationBehavior.SetSourceUri(this.originalGif, new Uri(filename));
                this.generationSettings = new GenerationSettings(SixLabors.ImageSharp.Image.Load<Rgba32>(filename), loadedName);
                this.frameCountLabel.Content = "FrameCount: " + generationSettings.Frames.ToString();
                this.frameSlider.Maximum = Math.Min(generationSettings.Frames, 64);
                this.startSlider.Minimum = 1;
                this.endSlider.Minimum = 1;
                this.startSlider.Value = 0;
                this.startSlider.Maximum = generationSettings.Frames;
                this.endSlider.Maximum = generationSettings.Frames;
                this.endSlider.Value = generationSettings.Frames;
                this.generate.IsEnabled = true;
                this.chromaBox.IsEnabled = true;
                this.logoff.IsEnabled = true;
                this.frameSlider.Value = this.frameSlider.Maximum;
                label2.Content = "1";
            }
            //var xD = MemoryDiagnostics.TotalUndisposedAllocationCount;
        }

        private async void generate_Click(object sender, RoutedEventArgs e)
        {
            if (this.generationSettings is null)
            {
                return;
            }
            generationResult?.Dispose();
            generationSettings.StartFrame = (int)startSlider.Value - 1;
            generationSettings.EndFrame = (int)endSlider.Value - 1;
            generationSettings.TargetFrameCount = (int)frameSlider.Value;
            generationSettings.CropSettings = null;
            generationSettings.ChromaSettings = null;
            if (cropBox.IsChecked == true && rect != null) {
                generationSettings.CropSettings = new Rect() { X = startPoint.X, Y = startPoint.Y, Width = rect.Width, Height = rect.Height };
            }
            if (chromaBox.IsChecked == true)
            {
                generationSettings.ChromaSettings = new ChromaSettings((ChromaType)this.chromaTypeBox.SelectedItem, chromaColor, (int)this.thresholdSlider.Value);
            }
            SpriteSheetBehaviour.SetSpriteSheet(this.resultBrush, null);
            this.IsEnabled = false;
            this.generateLabel.Content = "Generating...";
            generationResult = await Task.Run(() => { return EmojiGeneration.EmojiGeneration.GenerateEmoji(generationSettings); });
            SpriteSheetBehaviour.SetSpriteSheet(this.resultBrush, generationResult.Image, generationResult.Frames, generationResult.Columns, generationResult.Columns, generationResult.FPS, 256, 256);
            this.save.IsEnabled = true;
            this.upload.IsEnabled = true;
            this.generateLabel.Content = "";
            this.IsEnabled = true;
        }

        private void cropBox_Checked(object sender, RoutedEventArgs e)
        {
            rect = new System.Windows.Shapes.Rectangle
            {
                Stroke = Brushes.LightBlue,
                StrokeThickness = 2
            };
            startPoint = new System.Windows.Point(0, 0);
            Canvas.SetLeft(rect, startPoint.X);
            Canvas.SetTop(rect, startPoint.Y);
            canvas.Children.Add(rect);
            rect.Width = 256;
            rect.Height = 256;
        }

        private void cropBox_Unchecked(object sender, RoutedEventArgs e)
        {
            rect = null;
            canvas.Children.Clear();
        }

        private void save_Click(object sender, RoutedEventArgs e)
        {
            if (generationResult is not null)
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    DefaultExt = ".png",
                    Filter = @"PNG|*.png",
                    FileName = loadedName + "_" + generationResult.Frames + "frames_" + generationResult.FPS + "fps.png"
                };
                bool? result = dialog.ShowDialog();
                if (result == true)
                {
                    generationResult.Image.SaveAsPng(dialog.FileName);
                }
            }
            
        }

        private void startSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.endSlider != null && this.frameSlider != null && label2 != null) {
                int oldval = (int)frameSlider.Value;
                if (startSlider.Value > endSlider.Value)
                {
                    startSlider.Value = endSlider.Value;
                }
                this.frameSlider.Maximum = Math.Min(64, endSlider.Value - startSlider.Value + 1);
                frameSlider.Value = Math.Min(frameSlider.Maximum, oldval);
                label2.Content = startSlider.Value.ToString();
            }
        }

        private void endSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.startSlider != null && this.frameSlider != null && label2_Copy != null)
            {
                int oldval = (int)frameSlider.Value;
                if (endSlider.Value < startSlider.Value)
                {
                    endSlider.Value = startSlider.Value;
                }
                this.frameSlider.Maximum = Math.Min(64, endSlider.Value - startSlider.Value + 1);
                frameSlider.Value = Math.Min(frameSlider.Maximum, oldval);
                label2_Copy.Content = endSlider.Value.ToString();
            }
        }

        private void frameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (label2_Copy1 != null)
            {
                label2_Copy1.Content = frameSlider.Value.ToString();
            }
        }

        private void chromaButton_Click(object sender, RoutedEventArgs e)
        {
            this.chromaPicker = true;
            this.chromaButton.IsEnabled = false;
            this.open.IsEnabled = false;
            this.generate.IsEnabled = false;
            this.save.IsEnabled = false;
            this.upload.IsEnabled = false;
            this.chromaBox.IsEnabled = false;
            this.logoff.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Cross;
            AnimationBehavior.GetAnimator(originalGif).Pause();
        }

        private void chromaBox_Checked(object sender, RoutedEventArgs e)
        {
            this.chromaButton.Visibility = Visibility.Visible;
            this.chromaButton.IsEnabled = true;
            this.chromaButton.Background = Brushes.Green;
            this.thresholdSlider.Value = 30;
            this.chromaColor = SixLabors.ImageSharp.Color.Green.ToPixel<Rgba32>();
            this.threshold_label.Visibility = Visibility.Visible;
            this.thresholdSlider.Visibility = Visibility.Visible;
            this.chromaTypeLabel.Visibility = Visibility.Visible;
            this.chromaTypeBox.Visibility = Visibility.Visible;
        }

        private void chromaBox_Unchecked(object sender, RoutedEventArgs e)
        {
            this.chromaButton.Visibility = Visibility.Hidden;
            this.chromaButton.IsEnabled = false;
            this.threshold_label.Visibility = Visibility.Hidden;
            this.thresholdSlider.Visibility = Visibility.Hidden;
            this.chromaTypeLabel.Visibility = Visibility.Hidden;
            this.chromaTypeBox.Visibility = Visibility.Hidden;
        }

        private void upload_Click(object sender, RoutedEventArgs e)
        {
            if (this.generationResult == null) {
                return;
            }
            this.IsEnabled = false;
            AuthResult? authResult = EmojiGeneration.EmojiGeneration.UploadEmoji(generationResult);
            if (authResult != null) {
                this.loggedLabel.Content = authResult.CurrentUser?.DisplayName;
                this.logoff.Visibility = Visibility.Visible;
            }
            this.IsEnabled = true;
        }

        private void logoff_Click(object sender, RoutedEventArgs e)
        {
            Authentication.Instance.LogOff();
            this.loggedLabel.Content = "Not logged in";
            this.logoff.Visibility = Visibility.Hidden;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            chromaTypeBox.SelectedIndex = 0;
        }
    }
}