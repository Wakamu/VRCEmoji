using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Windows;

namespace VRCEMoji.EmojiGeneration
{
    internal class EmojiGeneration
    {

        public static GenerationResult GenerateEmoji(GenerationSettings settings)
        {
            Image<Rgba32>[] frames = GetFrames(settings);
            int frameCount = frames.Length;
            int targetFrameCount = settings.TargetFrameCount;
            int toRemove = frameCount - targetFrameCount;
            int ratio = toRemove != 0 ? (int)Math.Round((double)frameCount / (double)toRemove) : 0;
            double realRatio = toRemove != 0 ? ((double)frameCount / (double)toRemove) : 0;
            int gridSize = settings.GridSize;
            Image<Rgba32>[] newFrames = new Image<Rgba32>[targetFrameCount];
            while (ratio < 2 && ratio != 0)
            {
                frames = Divise(frames);
                frameCount = frames.Length;
                toRemove = frameCount - targetFrameCount;
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
                    if (settings.ChromaSettings != null)
                    {
                        ChromaKey(frames[i], settings.ChromaSettings);
                    }
                    if (settings.CropSettings != null)
                    {
                        // CropSettings is in source-image pixel coordinates (produced by
                        // MainWindow.CanvasToImagePixel).
                        Rect cropSettings = (Rect)settings.CropSettings;
                        var cropRect = new Rectangle(
                            (int)cropSettings.X,
                            (int)cropSettings.Y,
                            (int)cropSettings.Width,
                            (int)cropSettings.Height
                        );
                        var option = new ResizeOptions
                        {
                            Mode = settings.KeepRatio ? SixLabors.ImageSharp.Processing.ResizeMode.Pad : SixLabors.ImageSharp.Processing.ResizeMode.Stretch,
                            Size = new SixLabors.ImageSharp.Size(gridSize, gridSize)
                        };
                        frames[i].Mutate(
                            i => i.Crop(cropRect).Resize(option)
                        );
                    }
                    else
                    {
                        var option = new ResizeOptions
                        {
                            Mode = settings.KeepRatio ? SixLabors.ImageSharp.Processing.ResizeMode.Pad : SixLabors.ImageSharp.Processing.ResizeMode.Stretch,
                            Size = new SixLabors.ImageSharp.Size(gridSize, gridSize)
                        };
                        frames[i].Mutate(
                            i => i.Resize(option)
                        );
                    }
                    var optionZoom = new ResizeOptions
                    {
                        Mode = SixLabors.ImageSharp.Processing.ResizeMode.Crop,
                        Size = new SixLabors.ImageSharp.Size((int) (gridSize * settings.Zoom), (int)(gridSize * settings.Zoom)),
                    };
                    frames[i].Mutate(
                        i => i.Resize(optionZoom)
                    );
                    newFrames[j] = frames[i];
                    j++;
                }
            }
            int maxline = 1024 / gridSize;
            var result = new Image<Rgba32>(1024, 1024);
            int currentFrame = 0;
            foreach (var frame in newFrames)
            {
                result.Mutate(o => o
                    .DrawImage(frame, new SixLabors.ImageSharp.Point((currentFrame % maxline) * gridSize, (currentFrame / maxline) * gridSize), 1f)
                );
                currentFrame++;
            }
            foreach (var frame in frames)
            {
                frame.Dispose();
            }
            foreach (var frame in newFrames)
            {
                frame.Dispose();
            }
            SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            return new GenerationResult(result, settings.Name, targetFrameCount, settings.FPS, settings.generationType);

        }

        static Image<Rgba32>[] Divise(Image<Rgba32>[] list)
        {
            List<Image<Rgba32>> newList = [];
            for (int i = 0; i < list.Length; i++)
            {
                if (i % 2 != 0)
                {
                    newList.Add(list[i]);
                }
                else
                {
                    list[i].Dispose();
                }
            }
            return [.. newList];
        }

        static void ChromaKey(Image<Rgba32> image, ChromaSettings chromaSettings)
        {
            if (chromaSettings.ChromaType == ChromaType.HSV)
            {
                Hsv targetColor = ColorSpaceConverter.ToHsv(chromaSettings.ChromaColor);
                for (int i = 0; i < image.Width; i++)
                {
                    for (int j = 0; j < image.Height; j++)
                    {
                        Hsv pixelHSV = ColorSpaceConverter.ToHsv(image[i, j]);
                        float hueDiff = Math.Abs(pixelHSV.H - targetColor.H);
                        float satDiff = Math.Abs(pixelHSV.S - targetColor.S);
                        float valDiff = Math.Abs(pixelHSV.V - targetColor.V);
                        if (hueDiff <= chromaSettings.Threshold && satDiff <= chromaSettings.Threshold && valDiff <= chromaSettings.Threshold)
                        {
                            image[i, j] = SixLabors.ImageSharp.Color.Transparent;
                        }
                    }
                }
                return;
            }
            for (int i = 0; i < image.Width; i++)
            {
                for (int j = 0; j < image.Height; j++)
                {
                    Rgba32 pixelRGB = image[i, j];
                    int rgbDiff =
                        Math.Abs(pixelRGB.R - chromaSettings.ChromaColor.R) +
                        Math.Abs(pixelRGB.G - chromaSettings.ChromaColor.G) +
                        Math.Abs(pixelRGB.B - chromaSettings.ChromaColor.B);
                    if (rgbDiff <= chromaSettings.Threshold)
                    {
                        image[i, j] = SixLabors.ImageSharp.Color.Transparent;
                    }
                }
            }
        }

        static Image<Rgba32>[] GetFrames(GenerationSettings settings)
        {
            int numberOfFrames = settings.Frames;
            Image<Rgba32>[] frames = new Image<Rgba32>[settings.KeptFrames];
            int j = 0;
            for (int i = 0; i < numberOfFrames; i++)
            {
                if (i >= settings.StartFrame && i <= settings.EndFrame)
                {
                    frames[j] = settings.Image.Frames.CloneFrame(i);
                    j++;
                }
            }
            return frames;
        }
    }
}
