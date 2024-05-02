using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Windows;

namespace VRCEMoji.EmojiGeneration
{
    internal class GenerationSettings(Image<Rgba32> image, string name, Rect? cropSettings = null, ChromaSettings? chromaSettings = null): IDisposable
    {
        public string Name { get; set; } = name;

        public Image<Rgba32> Image { get; set; } = image;

        public Rect? CropSettings { get; set; } = cropSettings;

        public ChromaSettings? ChromaSettings { get; set; } = chromaSettings;

        public int StartFrame { get; set; } = 1;

        public int EndFrame { get; set; } = image.Frames.Count;

        public int TargetFrameCount { get; set; } = image.Frames.Count;

        public int TargetDuration { 
            get { return KeptFrames * FrameDelay; }
        }

        public int KeptFrames
        {
            get { return EndFrame - StartFrame + 1; }
        }

        public int GridSize
        {
            get { return TargetFrameCount <= 4 ? 512 : (TargetFrameCount <= 16 ? 256 : 128); }
        }

        public int Frames { 
            get { return Image.Frames.Count; }
        }

        public int FrameDelay
        {
            get { 
                int delay = Image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay * 10;
                return delay == 0 ? 106 : delay;
            }
        }

        public int FPS
        {
            get { 
                return (int)Math.Round((double)TargetFrameCount / ((double)TargetDuration / 1000d));
            }
        }

        public void Dispose()
        {
            Image.Dispose();
        }
    }
}
