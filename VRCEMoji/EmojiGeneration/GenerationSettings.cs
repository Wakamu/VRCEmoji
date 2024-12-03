using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.Serialization;
using System.Windows;

namespace VRCEMoji.EmojiGeneration
{
    internal class GenerationSettings(Image<Rgba32> image, string name, Rect? cropSettings = null, ChromaSettings? chromaSettings = null): IDisposable
    {
        public string Name { get; set; } = name;

        public Image<Rgba32> Image { get; set; } = image;

        public Rect? CropSettings { get; set; } = cropSettings;

        public GenerationType generationType = GenerationType.Emoji;

        public ChromaSettings? ChromaSettings { get; set; } = chromaSettings;

        public GenerationMode GenerationMode { get; set; } = GenerationMode.Fluidity;

        public int StartFrame { get; set; } = 1;

        public int EndFrame { get; set; } = image.Frames.Count;
        public bool KeepRatio { get; set; } = false;

        public int TargetFrameCount {
            get
            {
                return GenerationMode switch
                {
                    GenerationMode.Fluidity => Math.Min(64, KeptFrames),
                    GenerationMode.Balance => Math.Min(16, KeptFrames),
                    GenerationMode.Quality => Math.Min(4, KeptFrames),
                    _ => Math.Min(64, KeptFrames),
                };
            }
        }

        public int TargetDuration { 
            get { return KeptFrames * FrameDelay; }
        }

        public int KeptFrames
        {
            get { return EndFrame - StartFrame + 1; }
        }

        public int GridSize
        {
            get { return TargetFrameCount < 2 ? 1024 : TargetFrameCount <= 4 ? 512 : (TargetFrameCount <= 16 ? 256 : 128); }
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
                return Math.Max(1, (int)Math.Round((double)TargetFrameCount / ((double)TargetDuration / 1000d)));
            }
        }

        public void Dispose()
        {
            Image.Dispose();
        }
    }

    public enum GenerationMode
    {
        [EnumMember(Value = "quality")]
        Quality = 1,

        [EnumMember(Value = "balance")]
        Balance = 2,

        [EnumMember(Value = "fluidity")]
        Fluidity = 3,
    }

    public enum GenerationType
    {
        [EnumMember(Value = "emoji")]
        Emoji = 1,

        [EnumMember(Value = "sticker")]
        Sticker = 2,
    }
}
