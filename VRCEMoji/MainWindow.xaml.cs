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
using VRCEMoji.Overlays;
using System.Reflection;

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
        private string? _pendingReplacementId;
        private bool _manageNeedsRefresh;

        public MainWindow()
        {
            InitializeComponent();
            CheckUpdate();
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
            manageView.FileClicked += ManageView_FileClicked;
            _manageNeedsRefresh = true;
            RestoreWindowState();
        }

        private async void CheckUpdate()
        {
            try
            {
                GitHubClient client = new GitHubClient(new ProductHeaderValue("VRCEmoji"));

                Release latest = await client.Repository.Release.GetLatest("Wakamu", "VRCEmoji");

                string latestVersion = latest.TagName.TrimStart('v');
                string currentVersion = GetCurrentVersion();

                if (Version.Parse(latestVersion) > Version.Parse(currentVersion))
                {
                    var result = MessageBox.Show(
                        $"Update available ({latest.TagName}). Do you want to download it?",
                        "Update Available!",
                        MessageBoxButton.YesNo
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = latest.HtmlUrl,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // optional: log ex
            }
        }

        private string GetCurrentVersion()
        {
            return Assembly
                .GetExecutingAssembly()
                .GetName()
                .Version?
                .ToString();
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
            ConvertModeBox.IsEnabled = enabled;
        }

        /// <summary>
        /// Returns the rectangle (in canvas coordinates) where the source image actually renders,
        /// accounting for the Image control's Stretch="Uniform" letterbox/pillarbox layout.
        /// Returns null when no image is loaded or the canvas has zero size.
        /// </summary>
        private Rect? GetImageRenderRect()
        {
            if (generationSettings is null) return null;
            double canvasW = canvas.ActualWidth;
            double canvasH = canvas.ActualHeight;
            double frameW = generationSettings.Image.Width;
            double frameH = generationSettings.Image.Height;
            if (canvasW <= 0 || canvasH <= 0 || frameW <= 0 || frameH <= 0) return null;
            double scale = Math.Min(canvasW / frameW, canvasH / frameH);
            double renderedW = frameW * scale;
            double renderedH = frameH * scale;
            return new Rect((canvasW - renderedW) / 2, (canvasH - renderedH) / 2, renderedW, renderedH);
        }

        /// <summary>
        /// Converts a point in canvas coordinates to a point in source-image pixel coordinates.
        /// Clamps to [0, frame.Width] x [0, frame.Height] so drags into the letterbox margin
        /// resolve to the nearest image edge. Returns (0, 0) if no image is loaded.
        /// </summary>
        private System.Windows.Point CanvasToImagePixel(System.Windows.Point canvasPoint)
        {
            var render = GetImageRenderRect();
            if (!render.HasValue || generationSettings is null)
                return new System.Windows.Point(0, 0);
            double frameW = generationSettings.Image.Width;
            double frameH = generationSettings.Image.Height;
            double scale = render.Value.Width / frameW;
            double px = (canvasPoint.X - render.Value.X) / scale;
            double py = (canvasPoint.Y - render.Value.Y) / scale;
            return new System.Windows.Point(
                Math.Max(0, Math.Min(px, frameW)),
                Math.Max(0, Math.Min(py, frameH)));
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
                var imgPx = CanvasToImagePixel(e.GetPosition(canvas));
                int frameIndex = AnimationBehavior.GetAnimator(originalGif)?.CurrentFrameIndex ?? 0;
                var currentFrame = this.generationSettings.Image.Frames[frameIndex];
                int sampleX = Math.Min((int)imgPx.X, currentFrame.Width - 1);
                int sampleY = Math.Min((int)imgPx.Y, currentFrame.Height - 1);
                var rgbColor = currentFrame[sampleX, sampleY];
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
                // Convert the crop rectangle from canvas coordinates to source-image pixel
                // coordinates. Read the rect's actual canvas-space top-left rather than
                // startPoint so drags up-and-left (where startPoint is the bottom-right) also
                // produce a correct crop.
                var canvasTL = new System.Windows.Point(Canvas.GetLeft(rect), Canvas.GetTop(rect));
                var canvasBR = new System.Windows.Point(canvasTL.X + rect.Width, canvasTL.Y + rect.Height);
                var imgTL = CanvasToImagePixel(canvasTL);
                var imgBR = CanvasToImagePixel(canvasBR);
                double cropW = imgBR.X - imgTL.X;
                double cropH = imgBR.Y - imgTL.Y;
                if (cropW > 0 && cropH > 0)
                {
                    generationSettings.CropSettings = new Rect(imgTL.X, imgTL.Y, cropW, cropH);
                }
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
            // Default the rect to the source image's rendered area inside the canvas, so the
            // initial selection matches what the user sees rather than the full (possibly
            // letterboxed) canvas area.
            var renderRect = GetImageRenderRect() ?? new Rect(0, 0,
                canvas.ActualWidth > 0 ? canvas.ActualWidth : 256,
                canvas.ActualHeight > 0 ? canvas.ActualHeight : 256);
            startPoint = new System.Windows.Point(renderRect.X, renderRect.Y);
            Canvas.SetLeft(rect, renderRect.X);
            Canvas.SetTop(rect, renderRect.Y);
            canvas.Children.Add(rect);
            rect.Width = renderRect.Width;
            rect.Height = renderRect.Height;
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
                statusOverlay.Visibility == Visibility.Visible ||
                editOverlay.Visibility == Visibility.Visible)
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
                    // Server already rejected this config — no point in calling
                    // /logout with a cookie the server just declined.
                    Authentication.Instance.ClearLocal();
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
                var allFiles = Enumerable.Empty<ManagedFile>(); ;
                if (generationResult.GenerationType == GenerationType.Emoji)
                {
                    var emojiTask = Task.Run(() => fileApi.GetFiles("emoji", 100, 0));
                    var animatedTask = Task.Run(() => fileApi.GetFiles("emojianimated", 100, 0));
                    await Task.WhenAll(emojiTask, animatedTask);
                    allFiles = [.. emojiTask.Result, .. animatedTask.Result];
                } else
                {
                    var stickerTask = Task.Run(() => fileApi.GetFiles("sticker", 100, 0));
                    await Task.WhenAll(stickerTask);
                    allFiles = [.. stickerTask.Result];
                }


                if (allFiles.Count() >= 18)
                {
                    var replaceResult = await replaceOverlay.ShowAsync(allFiles.ToList());
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

                // Handle pending replacement: delete old file and switch back to Manage
                if (_pendingReplacementId != null)
                {
                    try
                    {
                        statusOverlay.ShowLoading("Removing old file...");
                        await Task.Run(() => fileApi.DeleteFile(_pendingReplacementId));
                        _pendingReplacementId = null;
                        _manageNeedsRefresh = true;
                        await statusOverlay.ShowSuccess("Replacement complete!");
                        manageTab.IsChecked = true;
                    }
                    catch (ApiException delEx)
                    {
                        _pendingReplacementId = null;
                        await statusOverlay.ShowError("Upload succeeded but failed to remove old file: " + delEx.Message);
                    }
                }
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
            // Fire-and-forget: UI updates immediately; the best-effort server
            // /logout call runs in the background with its own 5s cap.
            _ = Authentication.Instance.LogOffAsync();
            SetLoggedOutState();
            manageView.ShowNotLoggedIn();
        }

        private void SetLoggedInState(string displayName)
        {
            loggedLabel.Text = displayName;
            logoff.Visibility = Visibility.Visible;
            loginDot.Fill = (SolidColorBrush)FindResource("StatusSuccessBrush");
            _manageNeedsRefresh = true;
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

        // ══════════════ TAB SWITCHING ══════════════

        private void createTab_Checked(object sender, RoutedEventArgs e)
        {
            if (createContent == null || manageView == null) return;
            createContent.Visibility = Visibility.Visible;
            manageView.Visibility = Visibility.Collapsed;
        }

        private async void manageTab_Checked(object sender, RoutedEventArgs e)
        {
            if (createContent == null || manageView == null) return;
            createContent.Visibility = Visibility.Collapsed;
            manageView.Visibility = Visibility.Visible;

            if (Authentication.Instance.StoredConfig == null)
            {
                manageView.ShowNotLoggedIn();
                return;
            }

            if (_manageNeedsRefresh)
            {
                _manageNeedsRefresh = false;
                var fileApi = CreateFileApi();
                if (fileApi != null)
                {
                    await manageView.LoadFilesAsync(fileApi);
                }
            }
        }

        private async void ManageView_FileClicked(ManagedFile file)
        {
            var result = await editOverlay.ShowAsync(file);

            var fileApi = CreateFileApi();
            if (fileApi == null) return;

            switch (result.Action)
            {
                case EditAction.Delete:
                    await HandleDeleteAsync(fileApi, file);
                    break;

                case EditAction.Save:
                    await HandleSaveMetadataAsync(fileApi, file, result);
                    break;

                case EditAction.ReplaceImage:
                    HandleReplaceImage(file);
                    break;
            }
        }

        private async Task HandleDeleteAsync(EmojiApi.EmojiApi fileApi, ManagedFile file)
        {
            statusOverlay.ShowLoading("Deleting...");
            try
            {
                await Task.Run(() => fileApi.DeleteFile(file.Id));
                await statusOverlay.ShowSuccess((file.IsSticker ? "Sticker" : "Emoji") + " deleted successfully!");
                _manageNeedsRefresh = true;
                await manageView.LoadFilesAsync(fileApi);
            }
            catch (ApiException ex)
            {
                statusOverlay.Hide();
                await statusOverlay.ShowError("Failed to delete: " + ex.Message);
            }
        }

        private async Task HandleSaveMetadataAsync(EmojiApi.EmojiApi fileApi, ManagedFile file, EditResult editResult)
        {
            statusOverlay.ShowLoading("Downloading current image...");
            try
            {
                byte[] imageBytes = await Task.Run(() => fileApi.DownloadFileImage(file.ImageUrl));
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageBytes);

                statusOverlay.ShowLoading("Deleting old file...");
                await Task.Run(() => fileApi.DeleteFile(file.Id));

                statusOverlay.ShowLoading("Re-uploading with updated settings...");

                var tag = file.IsSticker ? "sticker" : (file.IsAnimated ? "emojianimated" : "emoji");
                var formParams = new Dictionary<string, string> { { "tag", tag } };

                if (file.IsSticker)
                {
                    formParams["maskTag"] = file.MaskTag ?? "square";
                }
                else
                {
                    formParams["frames"] = file.Frames.ToString();
                    formParams["framesOverTime"] = (editResult.UpdatedFPS ?? file.FramesOverTime).ToString();
                    formParams["maskTag"] = file.MaskTag ?? "square";

                    if (editResult.UpdatedAnimationStyle != null)
                    {
                        formParams["animationStyle"] = EnumHelper.GetMemberValue(editResult.UpdatedAnimationStyle.Value) ?? "aura";
                    }

                    if (editResult.UpdatedLoopStyle != null)
                    {
                        formParams["loopStyle"] = EnumHelper.GetMemberValue(editResult.UpdatedLoopStyle.Value) ?? "linear";
                    }
                }

                var requestOptions = new VRChat.API.Client.RequestOptions();
                requestOptions.HeaderParameters.Add("Accept", "*/*");
                requestOptions.FormParameters = formParams;
                requestOptions.FileParameters = [];

                string fileName = editResult.UpdatedName ?? file.Name;
                using (System.IO.Stream st = new System.IO.MemoryStream())
                {
                    image.SaveAsPng(st);
                    st.Position = 0;
                    requestOptions.FileParameters.Add(fileName, st);
                    requestOptions.Operation = "FilesApi.CreateFile";

                    var authKey = Authentication.Instance.ApiConfig?.GetApiKeyWithPrefix("auth");
                    if (!string.IsNullOrEmpty(authKey))
                    {
                        requestOptions.Cookies.Add(new System.Net.Cookie("auth", authKey));
                    }
                    var twoFactorKey = Authentication.Instance.StoredConfig?.TwoKey;
                    if (!string.IsNullOrEmpty(twoFactorKey))
                    {
                        requestOptions.Cookies.Add(new System.Net.Cookie("twoFactorAuth", twoFactorKey));
                    }

                    CustomApiClient uploadClient = new();
                    await Task.Run(() => uploadClient.PostEmoji<EmojiFile>("/file/image", requestOptions, Authentication.Instance.ApiConfig));
                }

                await statusOverlay.ShowSuccess((file.IsSticker ? "Sticker" : "Emoji") + " updated successfully!");
                _manageNeedsRefresh = true;
                await manageView.LoadFilesAsync(fileApi);
            }
            catch (Exception ex)
            {
                statusOverlay.Hide();
                await statusOverlay.ShowError("Failed to update: " + ex.Message);
            }
        }

        private void HandleReplaceImage(ManagedFile file)
        {
            _pendingReplacementId = file.Id;
            createTab.IsChecked = true;
            statusText.Text = "Replacing " + (file.IsSticker ? "sticker" : "emoji") + " — generate and upload a new image";
        }

        private EmojiApi.EmojiApi? CreateFileApi()
        {
            Configuration? config = Authentication.Instance.ApiConfig;
            if (config == null) return null;
            CustomApiClient apiClient = new();
            return new EmojiApi.EmojiApi(apiClient, apiClient, config);
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
            var path = System.IO.Path.Combine(EmojiApi.Authentication.StorageDir, "window.json");
            try { System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(state)); } catch { }
        }

        private void RestoreWindowState()
        {
            var path = System.IO.Path.Combine(EmojiApi.Authentication.StorageDir, "window.json");
            // One-shot migration: if the new path has nothing yet but the
            // legacy ProgramData location does, read from there.
            if (!System.IO.File.Exists(path))
            {
                var legacy = System.IO.Path.Combine(EmojiApi.Authentication.LegacyStorageDir, "window.json");
                if (System.IO.File.Exists(legacy))
                {
                    try { System.IO.File.Copy(legacy, path, overwrite: true); } catch { }
                    try { System.IO.File.Delete(legacy); } catch { }
                }
            }
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
