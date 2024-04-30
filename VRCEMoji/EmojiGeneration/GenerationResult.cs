namespace VRCEMoji.EmojiGeneration
{
    internal class GenerationResult(SixLabors.ImageSharp.Image image, int frames, int fps) : IDisposable
    {
        public SixLabors.ImageSharp.Image Image { get; set; } = image;

        public int Frames { get; set; } = frames;

        public int FPS { get; set; } = fps;

        public int Columns
        {
            get { return Frames <= 4 ? 2 : Frames <= 16 ? 4 : 8; }
        }

        public void Dispose()
        {
            Image.Dispose();
        }
    }
}
