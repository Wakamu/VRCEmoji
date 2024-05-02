namespace VRCEMoji.EmojiGeneration
{
    public class GenerationResult(SixLabors.ImageSharp.Image image, string name, int frames, int fps) : IDisposable
    {
        public string Name { get; set; } = name;

        public string FormatedName {
            get {  return Name + "_" + Frames + "frames_" + FPS + "fps.png"; }
        }

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
