using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XamlAnimatedGif;
using System.Windows.Media;
using VRCEMoji.EmojiApi;
using VRCEMoji.EmojiGeneration;
using Octokit;
using System.Diagnostics;
using VRChat.API.Api;
using VRChat.API.Client;
using VRChat.API.Model;
using VRCEmoji.EmojiApi;

using Configuration = VRChat.API.Client.Configuration;
using ApiException = VRChat.API.Client.ApiException;

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
            checkUpdate();
            AnimationBehavior.SetCacheFramesInMemory(this.originalGif, true);
            StoredConfig? authConfig = Authentication.Instance.StoredConfig;
            if (authConfig != null)
            {
                SetLoggedInState(authConfig.DisplayName);
            }
            chromaTypeBox.ItemsSource = Enum.GetValues(typeof(ChromaType)).Cast<ChromaType>();
            ConvertModeBox.ItemsSource = Enum.GetValues(typeof(GenerationMode)).Cast<GenerationMode>();
            generationTypeBox.ItemsSource = Enum.GetValues(typeof(GenerationType)).Cast<GenerationType>();
            generationTypeBox.SelectedItem = GenerationType.Emoji;
            _instance = this;
            RestoreWindowState();
        }

        private async void checkUpdate()
        {
            GitHubClient updateClient = new GitHubClient(new ProductHeaderValue("VRCEmoji"));
            try
            {
                Release latest = await updateClient.Repository.Release.GetLatest("Wakamu", "VRCEmoji");
                if (latest.TagName != "v1.10.0")
                {
                    if (MessageBox.Show("Update available (" + latest.TagName + "). Do you want to download it?", "Update Available!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        string url = "http://github.com/Wakamu/VRCEmoji/releases/latest";
                        Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                    }
                }
            }
            catch
            {
            }
        }

        public static MainWindow? Instance { get { return _instance; } }

        private void ToggleSection_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string targetName)
            {
                var content = (StackPanel?)this.FindName(targetName);
                if (content == null) return;

                var header = (TextBlock)border.Child;
                if (content.Visibility == Visibility.Visible)
                {
                    content.Visibility = Visibility.Collapsed;
                    header.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));

                    header.Inlines.Clear();
                    header.Inlines.Add(new System.Windows.Documents.Run("\u25B6 "));
                    header.Inlines.Add(new System.Windows.Documents.Run(GetSectionName(targetName)));
                }
                else
                {
                    content.Visibility = Visibility.Visible;
                    header.Foreground = (SolidColorBrush)FindResource("AccentBrush");

                    header.Inlines.Clear();
                    header.Inlines.Add(new System.Windows.Documents.Run("\u25BC "));
                    header.Inlines.Add(new System.Windows.Documents.Run(GetSectionName(targetName)));
                }
            }
        }

        private static string GetSectionName(string targetName)
        {
            return targetName switch
            {
                "framesContent" => "FRAMES",
                "generationContent" => "GENERATION",
                "effectsContent" => "EFFECTS",
                "chromaContent" => "CHROMA KEY",
                _ => ""
            };
        }

        private void thresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (thresholdValue != null)
            {
                thresholdValue.Text = ((int)e.NewValue).ToString();
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            open.IsEnabled = enabled;
            generate.IsEnabled = enabled;
            save.IsEnabled = enabled;
            upload.IsEnabled = enabled;
            chromaBox.IsEnabled = enabled;
            chromaButton.IsEnabled = enabled;
            logoff.IsEnabled = enabled;
            ConvertModeBox.IsEnabled = enabled;
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.chromaPicker && e.ChangedButton == MouseButton.Right)
            {
                CancelChromaPicker();
                return;
            }
            if (this.chromaPicker && this.generationSettings != null)
            {
                System.Windows.Point p = e.GetPosition(canvas);
                int frameIndex = AnimationBehavior.GetAnimator(originalGif)?.CurrentFrameIndex ?? 0;
                var currentFrame = this.generationSettings.Image.Frames[frameIndex];
                double cropWRatio = (double)canvas.ActualWidth / currentFrame.Width;
                double cropHRatio = (double)canvas.ActualHeight / currentFrame.Height;
                var rgbColor = currentFrame[Math.Min((int)(p.X / cropWRatio), currentFrame.Width - 1), Math.Min((int)(p.Y / cropHRatio), currentFrame.Height - 1)];
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
                SetControlsEnabled(true);
                AnimationBehavior.GetAnimator(originalGif)?.Play();
            }
            else if (this.cropBox.IsChecked == true)
            {
                rect = null;
                canvas.Children.Clear();
                startPoint = e.GetPosition(canvas);

                rect = new System.Windows.Shapes.Rectangle
                {
                    Stroke = new SolidColorBrush(Colors.Purple),
                    StrokeDashArray = new DoubleCollection(new double[] { 4, 2 }),
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
                Filter = "Image File|*.gif;*.jpg;*.jpeg;*.png;*.webp"
            };
            if (dialog.ShowDialog() == true)
            {
                LoadFile(dialog.FileName);
            }
        }

        private void LoadFile(string filename)
        {
            generationSettings?.Dispose();
            loadedName = System.IO.Path.GetFileNameWithoutExtension(filename);
            AnimationBehavior.SetSourceUri(originalGif, new Uri(filename));
            generationSettings = new GenerationSettings(SixLabors.ImageSharp.Image.Load<Rgba32>(filename), loadedName);
            sourceMetadata.Text = generationSettings.Frames + " frames";
            startSlider.Minimum = 1;
            endSlider.Minimum = 1;
            startSlider.Value = 0;
            startSlider.Maximum = generationSettings.Frames;
            endSlider.Maximum = generationSettings.Frames;
            endSlider.Value = generationSettings.Frames;
            generate.IsEnabled = true;
            chromaBox.IsEnabled = true;
            ConvertModeBox.IsEnabled = true;
            ConvertModeBox.SelectedItem = GenerationMode.Fluidity;
            label2.Text = "1";
            sourceEmpty.Visibility = Visibility.Collapsed;
        }

        private void SourcePreview_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    var ext = System.IO.Path.GetExtension(files[0]).ToLowerInvariant();
                    if (ext is ".gif" or ".png" or ".jpg" or ".jpeg" or ".webp")
                    {
                        e.Effects = DragDropEffects.Copy;
                    }
                }
            }
            e.Handled = true;
        }

        private void SourcePreview_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    var ext = System.IO.Path.GetExtension(files[0]).ToLowerInvariant();
                    if (ext is ".gif" or ".png" or ".jpg" or ".jpeg" or ".webp")
                    {
                        LoadFile(files[0]);
                    }
                }
            }
        }

        private async void generate_Click(object sender, RoutedEventArgs e)
        {
            if (this.generationSettings is null)
            {
                return;
            }
            generationResult?.Dispose();
            generationSettings.generationType = (GenerationType)generationTypeBox.SelectedItem;
            generationSettings.StartFrame = (int)startSlider.Value - 1;
            generationSettings.EndFrame = generationSettings.generationType == GenerationType.Emoji ? (int)endSlider.Value - 1 : generationSettings.StartFrame;
            generationSettings.GenerationMode = (GenerationMode)ConvertModeBox.SelectedItem;
            generationSettings.CropSettings = null;
            generationSettings.ChromaSettings = null;
            generationSettings.KeepRatio = keepRatio.IsChecked == true;
            generationSettings.Zoom = zoomSlider.Value;
            if (cropBox.IsChecked == true && rect != null)
            {
                // Scale crop coordinates from actual canvas size to the 256px space
                // that EmojiGeneration expects
                double canvasW = canvas.ActualWidth > 0 ? canvas.ActualWidth : 256;
                double canvasH = canvas.ActualHeight > 0 ? canvas.ActualHeight : 256;
                double scaleX = 256.0 / canvasW;
                double scaleY = 256.0 / canvasH;
                generationSettings.CropSettings = new Rect()
                {
                    X = startPoint.X * scaleX,
                    Y = startPoint.Y * scaleY,
                    Width = rect.Width * scaleX,
                    Height = rect.Height * scaleY
                };
            }
            if (chromaBox.IsChecked == true)
            {
                generationSettings.ChromaSettings = new ChromaSettings((ChromaType)this.chromaTypeBox.SelectedItem, chromaColor, (int)this.thresholdSlider.Value);
            }
            SpriteSheetBehaviour.SetSpriteSheet(this.resultBrush, null);
            SetControlsEnabled(false);
            generate.Content = "Generating...";
            statusText.Text = "Generating...";
            resultEmpty.Visibility = Visibility.Collapsed;
            generationResult = await Task.Run(() => { return EmojiGeneration.EmojiGeneration.GenerateEmoji(generationSettings); });

            SpriteSheetBehaviour.SetSpriteSheet(this.resultBrush, generationResult.Image, generationResult.Frames, generationResult.Columns, generationResult.Columns, generationResult.FPS, 256, 256);
            this.save.IsEnabled = true;
            this.upload.IsEnabled = true;
            generate.Content = "Generate";
            statusText.Text = "Ready";
            resultMetadata.Text = generationResult.Frames + " frames, " + generationResult.FPS + " fps";
            SetControlsEnabled(true);
                    }

        private void cropBox_Checked(object sender, RoutedEventArgs e)
        {
            rect = new System.Windows.Shapes.Rectangle
            {
                Stroke = new SolidColorBrush(Colors.Purple),
                StrokeDashArray = new DoubleCollection(new double[] { 4, 2 }),
                StrokeThickness = 2
            };
            startPoint = new System.Windows.Point(0, 0);
            Canvas.SetLeft(rect, startPoint.X);
            Canvas.SetTop(rect, startPoint.Y);
            canvas.Children.Add(rect);
            rect.Width = canvas.ActualWidth > 0 ? canvas.ActualWidth : 256;
            rect.Height = canvas.ActualHeight > 0 ? canvas.ActualHeight : 256;
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
            if (this.endSlider != null && label2 != null)
            {
                if (startSlider.Value > endSlider.Value)
                {
                    startSlider.Value = endSlider.Value;
                }
                label2.Text = startSlider.Value.ToString();
            }
        }

        private void endSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.startSlider != null && label2_Copy != null)
            {
                if (endSlider.Value < startSlider.Value)
                {
                    endSlider.Value = startSlider.Value;
                }
                label2_Copy.Text = endSlider.Value.ToString();
            }
        }

        private void chromaButton_Click(object sender, RoutedEventArgs e)
        {
            this.chromaPicker = true;
            SetControlsEnabled(false);
            Mouse.OverrideCursor = Cursors.Cross;
            AnimationBehavior.GetAnimator(originalGif)?.Pause();
        }

        private void CancelChromaPicker()
        {
            if (!chromaPicker) return;
            chromaPicker = false;
            Mouse.OverrideCursor = null;
            SetControlsEnabled(true);
            AnimationBehavior.GetAnimator(originalGif)?.Play();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && chromaPicker)
            {
                CancelChromaPicker();
                e.Handled = true;
                return;
            }


            if (loginOverlay.Visibility == Visibility.Visible ||
                inputOverlay.Visibility == Visibility.Visible ||
                uploadOverlay.Visibility == Visibility.Visible ||
                replaceOverlay.Visibility == Visibility.Visible ||
                statusOverlay.Visibility == Visibility.Visible)
                return;

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.O:
                        open_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.G:
                        if (generate.IsEnabled)
                            generate_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.S:
                        if (save.IsEnabled)
                            save_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                }
            }
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
            this.chromaButton.Visibility = Visibility.Collapsed;
            this.chromaButton.IsEnabled = false;
            this.threshold_label.Visibility = Visibility.Collapsed;
            this.thresholdSlider.Visibility = Visibility.Collapsed;
            this.chromaTypeLabel.Visibility = Visibility.Collapsed;
            this.chromaTypeBox.Visibility = Visibility.Collapsed;
        }

        private async Task<AuthResult> HandleAuthAsync()
        {
            AuthResult result = new();
            Configuration? config = Authentication.Instance.ApiConfig;

            string? loginError = null;
            while (config == null)
            {
                var loginResult = await loginOverlay.ShowAsync(loginError);
                if (!loginResult.Success)
                    return result;

                config = Authentication.Instance.CreateConfig(loginResult.Login, loginResult.Password);


                try
                {
                    CustomApiClient testClient = new();
                    AuthenticationApi testAuth = new(testClient, testClient, config);
                    testAuth.GetCurrentUserWithHttpInfo();
                }
                catch (ApiException)
                {
                    Authentication.Instance.LogOff();
                    config = null;
                    loginError = "Invalid username or password. Please try again.";
                    continue;
                }
            }

            CustomApiClient client = new();
            AuthenticationApi authApi = new(client, client, config);
            try
            {
                ApiResponse<CurrentUser> currentUserResp = authApi.GetCurrentUserWithHttpInfo();
                if (!Authentication.IsAuthed(currentUserResp))
                {
                    if (Authentication.RequiresEmail2FA(currentUserResp))
                    {
                        var inputResult = await inputOverlay.ShowAsync("Please verify with the OTP code sent to your email.");
                        if (inputResult.Success)
                        {
                            authApi.Verify2FAEmailCode(new TwoFactorEmailCode(inputResult.Answer));
                            currentUserResp = authApi.GetCurrentUserWithHttpInfo();
                        }
                        else
                            return result;
                    }
                    else
                    {
                        var inputResult = await inputOverlay.ShowAsync("Enter the code from your authenticator app.");
                        if (inputResult.Success)
                        {
                            authApi.Verify2FA(new TwoFactorAuthCode(inputResult.Answer));
                            currentUserResp = authApi.GetCurrentUserWithHttpInfo();
                        }
                        else
                            return result;
                    }
                }

                Authentication.Instance.FinalizeAuth(currentUserResp, config);
                CurrentUser? user = Authentication.Instance.ParseUser(currentUserResp);
                if (user is null)
                    return result;

                result.Success = true;
                result.CurrentUser = user;
                result.Configuration = config;
                return result;
            }
            catch (ApiException)
            {
                result.ErrorMessage = "Authentication failed. Please try again.";
                return result;
            }
        }

        private async void upload_Click(object sender, RoutedEventArgs e)
        {
            if (this.generationResult == null)
            {
                return;
            }
            SetControlsEnabled(false);
            statusText.Text = "Authenticating...";

            AuthResult authResult = await HandleAuthAsync();
            if (!authResult.Success || authResult.Configuration is null || authResult.CurrentUser is null)
            {
                if (authResult.ErrorMessage != null)
                {
                    await statusOverlay.ShowError(authResult.ErrorMessage);
                }
                SetControlsEnabled(true);
                statusText.Text = "Ready";
                return;
            }

            SetLoggedInState(authResult.CurrentUser.DisplayName);

            statusText.Text = "Uploading...";
            var uploadResult = await uploadOverlay.ShowAsync(generationResult);
            if (!uploadResult.Success || uploadResult.Settings is null)
            {
                SetControlsEnabled(true);
                statusText.Text = "Ready";
                return;
            }

            CustomApiClient apiClient = new();
            var fileApi = new EmojiApi.EmojiApi(apiClient, apiClient, authResult.Configuration);
            try
            {
                List<EmojiFile> files = generationResult.GenerationType == GenerationType.Emoji
                    ? fileApi.GetEmojiFiles(authResult.CurrentUser.Id, 100, 0)
                    : fileApi.GetStickerFiles(authResult.CurrentUser.Id, 100, 0);

                if (files.Count >= 18)
                {
                    var replaceResult = await replaceOverlay.ShowAsync(files);
                    if (replaceResult.Success)
                    {
                        fileApi.DeleteFile(replaceResult.SelectedId);
                    }
                    else
                    {
                        SetControlsEnabled(true);
                        statusText.Text = "Ready";
                        return;
                    }
                }

                statusOverlay.ShowLoading("Uploading " + (generationResult.GenerationType == GenerationType.Emoji ? "emoji" : "sticker") + "...");
                CreateEmojiRequest request = new(generationResult, uploadResult.Settings);
                await Task.Run(() => fileApi.CreateEmoji(request));
                await statusOverlay.ShowSuccess((generationResult.GenerationType == GenerationType.Emoji ? "Emoji" : "Sticker") + " uploaded successfully!");
            }
            catch (ApiException ex)
            {
                statusOverlay.Hide();
                await statusOverlay.ShowError(ex.Message);
            }

            SetControlsEnabled(true);
            statusText.Text = "Ready";
        }

        private async void LoginStatus_Click(object sender, MouseButtonEventArgs e)
        {
            if (Authentication.Instance.StoredConfig != null) return; // already logged in

            statusText.Text = "Authenticating...";
            AuthResult authResult = await HandleAuthAsync();
            if (authResult.Success && authResult.CurrentUser != null)
            {
                SetLoggedInState(authResult.CurrentUser.DisplayName);
            }
            statusText.Text = "Ready";
        }

        private void logoff_Click(object sender, RoutedEventArgs e)
        {
            Authentication.Instance.LogOff();
            SetLoggedOutState();
        }

        private void SetLoggedInState(string displayName)
        {
            loggedLabel.Text = displayName;
            logoff.Visibility = Visibility.Visible;
            loginDot.Fill = (SolidColorBrush)FindResource("StatusSuccessBrush");
        }

        private void SetLoggedOutState()
        {
            loggedLabel.Text = "Not logged in";
            logoff.Visibility = Visibility.Collapsed;
            loginDot.Fill = (SolidColorBrush)FindResource("TextDisabledBrush");
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            chromaTypeBox.SelectedIndex = 0;
        }

        private void ResetGenerationState(string frameLabel, bool endSliderEnabled,
            Visibility endSliderVisibility, Visibility convertModeVisibility, GenerationMode defaultMode)
        {
            this.startSlider.Minimum = 1;
            this.startSlider.Maximum = 1;
            this.label1.Text = frameLabel;
            this.endSlider.Minimum = 1;
            this.endSlider.Maximum = 1;
            this.endSlider.IsEnabled = endSliderEnabled;
            if (endSliderRow != null) this.endSliderRow.Visibility = endSliderVisibility;
            if (label1_Copy1 != null) this.label1_Copy1.Visibility = endSliderVisibility;
            this.startSlider.Value = 0;
            this.generate.IsEnabled = false;
            this.chromaBox.IsEnabled = false;
            this.logoff.IsEnabled = false;
            ConvertModeBox.IsEnabled = false;
            ConvertModeBox.Visibility = convertModeVisibility;
            ConvertModeBox.SelectedItem = defaultMode;
            sourceMetadata.Text = "";
            AnimationBehavior.SetSourceUri(this.originalGif, null);
            SpriteSheetBehaviour.SetSpriteSheet(this.resultBrush, null);
            if (sourceEmpty != null) sourceEmpty.Visibility = Visibility.Visible;
            if (resultEmpty != null) resultEmpty.Visibility = Visibility.Visible;
        }

        private void generationTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.save.IsEnabled = false;
            this.upload.IsEnabled = false;
            if ((GenerationType)generationTypeBox.SelectedItem == GenerationType.Emoji)
            {
                ResetGenerationState("Start:", true, Visibility.Visible, Visibility.Visible, GenerationMode.Fluidity);
            }
            else if ((GenerationType)generationTypeBox.SelectedItem == GenerationType.Sticker)
            {
                ResetGenerationState("Frame:", false, Visibility.Collapsed, Visibility.Collapsed, GenerationMode.Quality);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowState();
        }

        private void SaveWindowState()
        {
            var state = new { Left, Top, Width, Height, IsMaximized = WindowState == WindowState.Maximized };
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VRCEmoji", "window.json");
            try { System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(state)); } catch { }
        }

        private void RestoreWindowState()
        {
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VRCEmoji", "window.json");
            try
            {
                dynamic? state = Newtonsoft.Json.JsonConvert.DeserializeObject(System.IO.File.ReadAllText(path));
                if (state != null)
                {
                    double left = (double)state.Left;
                    double top = (double)state.Top;
                    double width = (double)state.Width;
                    double height = (double)state.Height;

                    // If the saved position is off-screen (e.g. monitor disconnected), center instead
                    bool offScreen = left < SystemParameters.VirtualScreenLeft
                        || top < SystemParameters.VirtualScreenTop
                        || left + width > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
                        || top + height > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

                    if (offScreen)
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }
                    else
                    {
                        Left = left;
                        Top = top;
                    }

                    Width = width;
                    Height = height;

                    if ((bool)state.IsMaximized)
                        WindowState = WindowState.Maximized;
                }
            }
            catch { }
        }
    }
}
