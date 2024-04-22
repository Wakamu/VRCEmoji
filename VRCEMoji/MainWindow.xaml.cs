using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XamlAnimatedGif;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Diagnostics;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.ColorSpaces;
using System.Windows.Media;

namespace VRCEMoji
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SixLabors.ImageSharp.Image<Rgba32> img;
        private int frameCount;
        private System.Windows.Point startPoint;
        private System.Windows.Shapes.Rectangle rect;
        private SixLabors.ImageSharp.Image lastResult;
        private int delay;
        private int finalFrameCount;
        private int finalDuration;
        private bool chromaPicker;
        private Hsv chromaColor;
        public string loadedName;

        public MainWindow()
        {
            InitializeComponent();
            AnimationBehavior.SetCacheFramesInMemory(this.originalGif, true);
            AnimationBehavior.SetCacheFramesInMemory(this.resultGif, true);
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.chromaPicker)
            {
                System.Windows.Point p = e.GetPosition(canvas);
                int frameIndex = AnimationBehavior.GetAnimator(originalGif).CurrentFrameIndex;
                var currentFrame = this.img.Frames[frameIndex];
                double cropWRatio = (double)256 / currentFrame.Width;
                double cropHRatio = (double)256 / currentFrame.Height;
                var rgbColor = currentFrame[Math.Min((int)(p.X / cropWRatio), currentFrame.Width), Math.Min((int)(p.Y / cropHRatio), currentFrame.Height)];
                chromaColor = ColorSpaceConverter.ToHsv(rgbColor);
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
                this.chromaBox.IsEnabled = true;
                AnimationBehavior.GetAnimator(originalGif).Play();
            }
            else if (this.cropBox.IsChecked == true)
            {
                rect = null;
                canvas.Children.Clear();
                startPoint = e.GetPosition(canvas);

                rect = new System.Windows.Shapes.Rectangle
                {
                    Stroke = System.Windows.Media.Brushes.LightBlue,
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
            }        }

        private void open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.FileName = "Image";
            dialog.DefaultExt = ".gif";
            dialog.Filter = "Gif (.gif)|*.gif";
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                if (this.img != null) {
                    this.img.Dispose();
                }   
                string filename = dialog.FileName;
                loadedName = System.IO.Path.GetFileNameWithoutExtension(filename);
                AnimationBehavior.SetSourceUri(this.originalGif, new Uri(filename));
                this.img = SixLabors.ImageSharp.Image.Load<Rgba32>(filename);
                frameCount = img.Frames.Count;
                delay = img.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay * 10;
                if (delay == 0)
                {
                    delay = 106;
                }
                this.frameCountLabel.Content = "FrameCount: " + frameCount.ToString();
                this.frameSlider.Maximum = Math.Min(frameCount, 64);
                this.startSlider.Minimum = 1;
                this.endSlider.Minimum = 1;
                this.startSlider.Value = 0;
                this.startSlider.Maximum = frameCount;
                this.endSlider.Maximum = frameCount;
                this.endSlider.Value = frameCount;
                this.generate.IsEnabled = true;
                this.chromaBox.IsEnabled = true;
                this.frameSlider.Value = this.frameSlider.Maximum;
                label2.Content = "1";
            }
            var xD = MemoryDiagnostics.TotalUndisposedAllocationCount;
        }

        private async void generate_Click(object sender, RoutedEventArgs e)
        {
            var systemPath = System.Environment.
                             GetFolderPath(
                                 Environment.SpecialFolder.CommonApplicationData
                             );
            if (lastResult != null)
            {
                lastResult.Dispose();
            }
            var complete = Path.Combine(systemPath, "VRCEmoji");
            System.IO.Directory.CreateDirectory(complete);
            int startFrame = (int)startSlider.Value - 1;
            int endFrame = (int)endSlider.Value - 1;
            int selectedValue = (int)frameSlider.Value;
            int duration = delay * frameCount;
            bool crop = cropBox.IsChecked == true;
            int keptFrames = (int)endSlider.Value - (int)startSlider.Value + 1;
            double durationRatio = (double)frameCount / (double)keptFrames;
            AnimationBehavior.SetSourceUri(this.resultGif, null);
            System.IO.File.Delete(complete + "\\preview.gif");
            finalDuration = (int)Math.Round((double)duration / durationRatio);
            finalFrameCount = selectedValue;
            int gridsize = selectedValue <= 4 ? 512 : (selectedValue <= 16 ? 256 : 128);
            this.open.IsEnabled = false;
            this.generate.IsEnabled = false;
            this.save.IsEnabled = false;
            this.frameSlider.IsEnabled = false;
            this.startSlider.IsEnabled = false;
            this.endSlider.IsEnabled = false;
            this.cropBox.IsEnabled = false;
            this.generateLabel.Content = "Generating...";
            this.resultGif.Source = null;
            int threshold = (int)this.thresholdSlider.Value;
            double cropX = startPoint.X;
            double cropY = startPoint.Y;
            double cropWidth = rect != null ? rect.Width : 0;
            double cropHeight = rect != null ? rect.Height : 0;
            bool useChroma = this.chromaBox.IsChecked ?? false;
            System.Windows.Shapes.Rectangle rectP = rect;
            lastResult = await Task.Run(() => {
                SixLabors.ImageSharp.Image<Rgba32>[] frames = getFrames(img, startFrame, endFrame);
                frameCount = frames.Length;
                int toRemove = frameCount - selectedValue;
                int ratio = toRemove != 0 ? (int)Math.Round((double)frameCount / (double)toRemove) : 0;
                double realRatio = toRemove != 0 ? ((double)frameCount / (double)toRemove) : 0;
                SixLabors.ImageSharp.Image<Rgba32>[] newFrames = new SixLabors.ImageSharp.Image<Rgba32>[selectedValue];
                while (ratio < 2 && ratio != 0)
                {
                    frames = Divise(frames);
                    frameCount = frames.Length;
                    toRemove = frameCount - selectedValue;
                    ratio = toRemove != 0 ? (int)Math.Round((double)frameCount / (double)toRemove) : 0;
                    realRatio = toRemove != 0 ? ((double)frameCount / (double)toRemove) : 0;
                }
                int removed = 0;
                int j = 0;
                for (int i = 0; i < frames.Length; i++)
                {
                    if (ratio != 0 && i == (int)Math.Round(realRatio * (double)removed) && removed != toRemove)
                    {
                        removed++;
                        continue;
                    }

                    if (j < newFrames.Length)
                    {
                        if (useChroma)
                        {
                            this.ChromaKey(frames[i], this.chromaColor, threshold);
                        }
                        if (crop)
                        {
                            double cropWRatio = (double)256 / frames[i].Width;
                            double cropHRatio = (double)256 / frames[i].Height;
                            System.Drawing.Rectangle cropRect = new System.Drawing.Rectangle(
                                new System.Drawing.Point((int)(cropX / cropWRatio), (int)(cropY / cropHRatio)),
                                new System.Drawing.Size((int)(cropWidth / cropWRatio), (int)(cropHeight / cropHRatio))
                            );
                            frames[i].Mutate(
                                i => i.Crop(new SixLabors.ImageSharp.Rectangle(cropRect.X, cropRect.Y, cropRect.Width, cropRect.Height)).Resize(gridsize, gridsize)    
                            );
                        }
                        else
                        {
                            frames[i].Mutate(
                                i => i.Resize(gridsize, gridsize)
                            );
                        }
                        newFrames[j] = frames[i];
                        j++;
                    }
                }
                using Image<Rgba32> gif = new(gridsize, gridsize);
                var gifMetaData = gif.Metadata.GetGifMetadata();
                int frameDelay = (int)Math.Round(((double)finalDuration / (double)finalFrameCount) / (double)10);
                gifMetaData.RepeatCount = 0;
                gifMetaData.ColorTableMode = GifColorTableMode.Local;
                GifFrameMetadata metadata = gif.Frames.RootFrame.Metadata.GetGifMetadata();
                metadata.FrameDelay = frameDelay;
                metadata.DisposalMethod = GifDisposalMethod.RestoreToBackground;
                metadata.HasTransparency = true;
                int maxline = 1024 / gridsize;
                var result = new SixLabors.ImageSharp.Image<Rgba32>(1024, 1024);
                int currentFrame = 0;
                
                foreach (var frame in newFrames)
                {
                    var gifFrame = new SixLabors.ImageSharp.Image<Rgba32>(gridsize, gridsize);
                    gifFrame.Mutate(o => o
                        .DrawImage(frame, 1f)
                    );
                    result.Mutate(o => o
                        .DrawImage(frame, new SixLabors.ImageSharp.Point((currentFrame % maxline) * gridsize, (currentFrame / maxline) * gridsize), 1f)
                    );
                    currentFrame++;
                    gifFrame.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = frameDelay;
                    gifFrame.Frames.RootFrame.Metadata.GetGifMetadata().DisposalMethod = GifDisposalMethod.RestoreToBackground;
                    gifFrame.Frames.RootFrame.Metadata.GetGifMetadata().HasTransparency = true;
                    gifFrame.Frames.RootFrame.Metadata.GetGifMetadata().TransparencyIndex = 0;
                    gif.Frames.AddFrame(gifFrame.Frames[0]);
                    gifFrame.Dispose();
                }
                foreach (var frame in frames)
                {
                    frame.Dispose();
                }
                foreach (var frame in newFrames)
                {
                    frame.Dispose();
                }
                gif.Frames.RemoveFrame(0);
                gif.SaveAsGif(complete + "\\preview.gif");
                gif.Dispose();
                return result;
            });
            AnimationBehavior.SetSourceUri(this.resultGif, new Uri(complete + "\\preview.gif"));
            this.open.IsEnabled = true;
            this.generate.IsEnabled = true;
            this.save.IsEnabled = true;
            this.frameSlider.IsEnabled = true;
            this.startSlider.IsEnabled = true;
            this.endSlider.IsEnabled = true;
            this.cropBox.IsEnabled = true;
            this.generateLabel.Content = "";
        }

        public SixLabors.ImageSharp.Image<Rgba32>[] Divise(SixLabors.ImageSharp.Image<Rgba32>[] list)
        {
            List<SixLabors.ImageSharp.Image<Rgba32>> newList = new List<SixLabors.ImageSharp.Image<Rgba32>>();
            for (int i = 0; i < list.Length; i++)
            {
                if (i % 2 != 0)
                {
                    newList.Add(list[i]);
                } else
                {
                    list[i].Dispose();
                }
            }
            return newList.ToArray();
        }

        void ChromaKey(SixLabors.ImageSharp.Image<Rgba32> image, Hsv chromaColor, int threshold)
        {
            for (int i = 0; i<image.Width; i++) {
                for (int j = 0; j < image.Height; j++) {
                    Hsv pixelHSV = ColorSpaceConverter.ToHsv(image[i, j]);
                    float hueDiff = Math.Abs(pixelHSV.H - chromaColor.H);
                    float satDiff = Math.Abs(pixelHSV.S - chromaColor.S);
                    float valDiff = Math.Abs(pixelHSV.V - chromaColor.V);
                    if (hueDiff <= threshold && satDiff <= threshold && valDiff <= threshold)
                    {
                        image[i, j] = SixLabors.ImageSharp.Color.Transparent; // Transparent for chroma key pixels
                    }
                }
            }
        }

        SixLabors.ImageSharp.Image<Rgba32>[] getFrames(SixLabors.ImageSharp.Image<Rgba32> originalImg, int frameStart, int frameEnd)
        {
            int numberOfFrames = originalImg.Frames.Count;
            long[] timePerCall = new long[frameEnd - frameStart + 1];
            SixLabors.ImageSharp.Image<Rgba32>[] frames = new SixLabors.ImageSharp.Image<Rgba32>[frameEnd - frameStart + 1];
            int j = 0;
            for (int i = 0; i < numberOfFrames; i++)
            {
                if (i >= frameStart && i <= frameEnd)
                {
                    frames[j] = originalImg.Frames.CloneFrame(i);
                    j++;
                }
            }
            return frames;
        }

        private void cropBox_Checked(object sender, RoutedEventArgs e)
        {
            rect = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.LightBlue,
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
            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.DefaultExt = ".png";
            dialog.Filter = @"PNG|*.png";
            dialog.FileName = loadedName + "_" + finalFrameCount + "frames_" + (int)Math.Round((double)1000 / ((double)finalDuration / (double)finalFrameCount)) + "fps.png";
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                lastResult.SaveAsPng(dialog.FileName);
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
            this.chromaBox.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Cross;
            AnimationBehavior.GetAnimator(originalGif).Pause();
        }

        private void chromaBox_Checked(object sender, RoutedEventArgs e)
        {
            this.chromaButton.Visibility = Visibility.Visible;
            this.chromaButton.IsEnabled = true;
            this.chromaButton.Background = Brushes.Green;
            this.chromaColor = ColorSpaceConverter.ToHsv(SixLabors.ImageSharp.Color.Green.ToPixel<Rgba32>());
            this.threshold_label.Visibility = Visibility.Visible;
            this.thresholdSlider.Visibility = Visibility.Visible;
        }

        private void chromaBox_Unchecked(object sender, RoutedEventArgs e)
        {
            this.chromaButton.Visibility = Visibility.Hidden;
            this.chromaButton.IsEnabled = false;
            this.threshold_label.Visibility = Visibility.Hidden;
            this.thresholdSlider.Visibility = Visibility.Hidden;
        }
    }

}