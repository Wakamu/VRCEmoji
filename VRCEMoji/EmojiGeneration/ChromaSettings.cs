using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.Serialization;

namespace VRCEMoji.EmojiGeneration
{
    internal class ChromaSettings(ChromaType chromaType, Rgba32 chromaColor, int threshold)
    {
        public ChromaType ChromaType { get; set; } = chromaType;

        public Rgba32 ChromaColor { get; set; } = chromaColor;

        public int Threshold { get; set; } = threshold;
    }

    public enum ChromaType
    {
        [EnumMember(Value = "hsv")]
        HSV = 1,

        [EnumMember(Value = "rgb")]
        RGB = 2,
    }
}
