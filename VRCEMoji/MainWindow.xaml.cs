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
using VRChat.API.Api;
using VRChat.API.Client;
using VRChat.API.Model;
using VRCEMoji.EmojiApi;
using System.Diagnostics;
using VRCEmoji.EmojiApi;
using Newtonsoft.Json;

namespace VRCEMoji
{
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
        private string storagePath;
        private StoredConfig? storedConfig;

        public MainWindow()
        {
            InitializeComponent();
            AnimationBehavior.SetCacheFramesInMemory(this.originalGif, true);
            AnimationBehavior.SetCacheFramesInMemory(this.resultGif, true);
            var systemPath = System.Environment.
                             GetFolderPath(
                                 Environment.SpecialFolder.CommonApplicationData
                             );
            storagePath = Path.Combine(systemPath, "VRCEmoji");
            System.IO.Directory.CreateDirectory(storagePath);
            this.storedConfig = this.getStoredConfig();
            if (this.storedConfig != null )
            {
                this.loggedLabel.Content = storedConfig.DisplayName;
                this.logoff.Visibility = Visibility.Visible;
            }
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
                this.logoff.IsEnabled = true;
                this.frameSlider.Value = this.frameSlider.Maximum;
                label2.Content = "1";
            }
            var xD = MemoryDiagnostics.TotalUndisposedAllocationCount;
        }

        private async void generate_Click(object sender, RoutedEventArgs e)
        {
            if (lastResult != null)
            {
                lastResult.Dispose();
            }
            int startFrame = (int)startSlider.Value - 1;
            int endFrame = (int)endSlider.Value - 1;
            int selectedValue = (int)frameSlider.Value;
            int duration = delay * frameCount;
            bool crop = cropBox.IsChecked == true;
            int keptFrames = (int)endSlider.Value - (int)startSlider.Value + 1;
            double durationRatio = (double)frameCount / (double)keptFrames;
            AnimationBehavior.SetSourceUri(this.resultGif, null);
            System.IO.File.Delete(storagePath + "\\preview.gif");
            finalDuration = (int)Math.Round((double)duration / durationRatio);
            finalFrameCount = selectedValue;
            int gridsize = selectedValue <= 4 ? 512 : (selectedValue <= 16 ? 256 : 128);
            this.open.IsEnabled = false;
            this.generate.IsEnabled = false;
            this.save.IsEnabled = false;
            this.upload.IsEnabled = false;
            this.frameSlider.IsEnabled = false;
            this.startSlider.IsEnabled = false;
            this.endSlider.IsEnabled = false;
            this.cropBox.IsEnabled = false;
            this.logoff.IsEnabled = false;
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
                                i => i.Crop(new SixLabors.ImageSharp.Rectangle(cropRect.X, cropRect.Y, cropRect.Width, cropRect.Height)).Resize(gridsize, gridsize, KnownResamplers.Box)
                            );
                        }
                        else
                        {
                            frames[i].Mutate(
                                i => i.Resize(gridsize, gridsize, KnownResamplers.Box)
                            );
                        }
                        newFrames[j] = frames[i];
                        j++;
                    }
                }
                using Image<Rgba32> gif = new(gridsize, gridsize);
                var gifMetaData = gif.Metadata.GetGifMetadata();
                int fps = (int)Math.Round((double)1000 / ((double)finalDuration / (double)finalFrameCount));
                int frameDelay = (int)Math.Round(((double)1000 / (double)fps)/(double)10);
                gifMetaData.RepeatCount = 0;
                GifFrameMetadata metadata = gif.Frames.RootFrame.Metadata.GetGifMetadata();
                int maxline = 1024 / gridsize;
                var result = new SixLabors.ImageSharp.Image<Rgba32>(1024, 1024);
                int currentFrame = 0;

                foreach (var frame in newFrames)
                {
                    var gifFrame = frame.Clone();
                    result.Mutate(o => o
                        .DrawImage(frame, new SixLabors.ImageSharp.Point((currentFrame % maxline) * gridsize, (currentFrame / maxline) * gridsize), 1f)
                    );
                    currentFrame++;
                    gifFrame.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = frameDelay;
                    if(useChroma)
                    {
                        gifFrame.Frames.RootFrame.Metadata.GetGifMetadata().HasTransparency = true;
                        gifFrame.Frames.RootFrame.Metadata.GetGifMetadata().DisposalMethod = GifDisposalMethod.RestoreToBackground;
                    }
                    gif.Frames.AddFrame(gifFrame.Frames.RootFrame);
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
                gif.SaveAsGif(storagePath + "\\preview.gif");
                gif.Dispose();
                return result;
            });
            AnimationBehavior.SetSourceUri(this.resultGif, new Uri(storagePath + "\\preview.gif"));
            this.open.IsEnabled = true;
            this.generate.IsEnabled = true;
            this.save.IsEnabled = true;
            this.upload.IsEnabled = true;
            this.frameSlider.IsEnabled = true;
            this.startSlider.IsEnabled = true;
            this.endSlider.IsEnabled = true;
            this.cropBox.IsEnabled = true;
            this.logoff.IsEnabled = true;
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
                        image[i, j] = SixLabors.ImageSharp.Color.Transparent;
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

        static bool requiresEmail2FA(ApiResponse<CurrentUser> resp)
        {
            if (resp.RawContent.Contains("emailOtp"))
            {
                return true;
            }

            return false;
        }

        static bool isAuthed(ApiResponse<CurrentUser> resp)
        {
            if (resp.RawContent.Contains("displayName"))
            {
                return true;
            }

            return false;
        }

        private StoredConfig? getStoredConfig()
        {
            if (System.IO.File.Exists(this.storagePath + "\\account.json"))
            {
                StoredConfig storedConfig = JsonConvert.DeserializeObject<StoredConfig>(System.IO.File.ReadAllText(this.storagePath + "\\account.json"));
                return storedConfig;
            }
            return null;
        }

        private VRChat.API.Client.Configuration? GetApiConfig()
        {
            VRChat.API.Client.Configuration config = new VRChat.API.Client.Configuration();
            config.UserAgent = "VRCEmoji/1.2.0 wakamu";
            if (this.storedConfig != null)
            {
                config.Username = storedConfig.Username;
                config.Password = storedConfig.Password;
                config.DefaultHeaders.Add("Cookie", "auth=" + storedConfig.Auth + ";twoFactorAuth=" + storedConfig.TwoKey);
            } else
            {
                LoginDialog loginDialog = new LoginDialog { Owner = this };
                if (loginDialog.ShowDialog() == true)
                {
                    config.Username = loginDialog.Login;
                    config.Password = loginDialog.Password;
                } else
                {
                    return null;
                }
            }
            return config;
        }

        private void CreateStoredConfig(VRChat.API.Client.Configuration config, string auth, string twoKey, string displayName)
        {
            System.IO.File.Delete(this.storagePath + "\\account.json");
            StoredConfig storedConfig = new StoredConfig { 
                Username = config.Username,
                Password = config.Password,
                Auth = auth,
                TwoKey = twoKey,
                DisplayName = displayName
            };
            System.IO.File.WriteAllText(this.storagePath + "\\account.json", JsonConvert.SerializeObject(storedConfig));
            this.storedConfig = storedConfig;
            this.loggedLabel.Content = storedConfig.DisplayName;
            this.logoff.Visibility = Visibility.Visible;
        }

        private AuthResult handleAuth()
        {
            VRChat.API.Client.Configuration? config = GetApiConfig();
            AuthResult result = new AuthResult();
            if (config == null)
            {
                return result;
            }
            bool logged = false;
            CustomApiClient client = new CustomApiClient();
            AuthenticationApi authApi = new AuthenticationApi(client, client, config);
            try
            {
                ApiResponse<CurrentUser> currentUserResp = authApi.GetCurrentUserWithHttpInfo();
                bool cancelOperation = false;
                logged = true;
                if (!isAuthed(currentUserResp) && !cancelOperation)
                {
                    if (requiresEmail2FA(currentUserResp))
                    {
                        InputDialog inputDialog = new InputDialog("Please verify with the OTP code sent to your email.");
                        inputDialog.Owner = this;
                        if (inputDialog.ShowDialog() == true)
                        {
                            authApi.Verify2FAEmailCode(new TwoFactorEmailCode(inputDialog.Answer));
                            currentUserResp = authApi.GetCurrentUserWithHttpInfo();
                        }
                        else
                        {
                            cancelOperation = true;
                        }
                    }
                    else
                    {
                        InputDialog inputDialog = new InputDialog("Please verify with your double authentication code.");
                        inputDialog.Owner = this;
                        if (inputDialog.ShowDialog() == true)
                        {
                            authApi.Verify2FA(new TwoFactorAuthCode(inputDialog.Answer));
                            currentUserResp = authApi.GetCurrentUserWithHttpInfo();
                        }
                        else { cancelOperation = true; }
                    }
                }
                if (cancelOperation)
                {
                    return result;
                }
                var authCookie = currentUserResp.Cookies.Find(x => x.Name == "auth");
                var f2aCookie = currentUserResp.Cookies.Find(x => x.Name == "twoFactorAuth");
                CurrentUser user = (CurrentUser)currentUserResp.Content;
                if (authCookie != null && f2aCookie != null)
                {
                    var auth = authCookie.Value;
                    var f2a = f2aCookie.Value;
                    CreateStoredConfig(config, auth, f2a, user.DisplayName);
                }
                result.Success = true;
                result.CurrentUser = user;
                result.Configuration = config;
                return result;
            } catch(ApiException)
            {
                result.ErrorMessage = logged ? "An error occured with the two factor authentication.": "Invalid username/password.";
                return result;
            }
        }    

        private void upload_Click(object sender, RoutedEventArgs e)
        {
            this.IsEnabled = false;
            AuthResult authResult = this.handleAuth();
            if (! authResult.Success)
            {
                if (authResult.ErrorMessage != null)
                {
                    MessageBox.Show(authResult.ErrorMessage);
                }
                this.IsEnabled = true;
                return;
            }

            CustomApiClient client = new CustomApiClient();
            var fileApi = new EmojiApi.EmojiApi(client, client, authResult.Configuration);
            try
            {
                List<EmojiFile> files = fileApi.GetEmojiFiles(authResult.CurrentUser.Id, 100, 0);
                UploadDialog uploadDialog = new UploadDialog();
                uploadDialog.Owner = this;
                if (uploadDialog.ShowDialog() == false)
                {
                    this.IsEnabled = true;
                    return;
                }
                UploadSettings uploadSettings = uploadDialog.Settings;
                if (files.Count >= 9)
                {
                    ReplaceDialog replaceDialog = new ReplaceDialog(files);
                    replaceDialog.Owner = this;
                    if (replaceDialog.ShowDialog() == true)
                    {
                        fileApi.DeleteFile(replaceDialog.SelectedId);
                    } else
                    {
                        this.IsEnabled = true;
                        return;
                    }
                }
                int fps = (int)Math.Round((double)1000 / ((double)finalDuration / (double)finalFrameCount));
                CreateEmojiRequest request = new(finalFrameCount, fps, this.lastResult);
                request.Name = loadedName + "_" + finalFrameCount + "frames_" + (int)Math.Round((double)1000 / ((double)finalDuration / (double)finalFrameCount)) + "fps.png";
                request.Extension = ".png";
                request.AnimationStyle = uploadSettings.AnimationStyle;
                request.LoopStyle = uploadSettings.LoopStyle;
                fileApi.CreateEmoji(this.lastResult, loadedName + "_" + finalFrameCount + "frames_" + (int)Math.Round((double)1000 / ((double)finalDuration / (double)finalFrameCount)) + "fps.png", request);
                MessageBox.Show("Emoji uploaded successfully!");
            }
            catch (ApiException ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
            this.IsEnabled = true;
        }

        private void logoff_Click(object sender, RoutedEventArgs e)
        {
            System.IO.File.Delete(this.storagePath + "\\account.json");
            this.storedConfig = null;
            this.loggedLabel.Content = "Not logged in";
            this.logoff.Visibility = Visibility.Hidden;
        }
    }

}