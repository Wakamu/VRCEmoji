using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VRCEMoji.EmojiApi;

namespace VRCEMoji
{
    internal class SpriteSheetBehaviour
    {
        public static List<SpriteSheetBehaviour> behaviours = [];

        private readonly ImageBrush brush;
        private DateTime instanceTime;
        private readonly int frames, rows, columns, width, height;
        private int fps;
        private readonly MatrixTransform transform;
        private LoopStyle loopStyle;

        public SpriteSheetBehaviour(ImageBrush brush, int frames, int rows, int columns, int displayWidth, int displayHeight, int fps)
        {
            this.brush = brush;
            this.instanceTime = DateTime.Now;
            this.frames = frames;
            this.rows = rows;
            this.columns = columns;
            this.width = displayWidth;
            this.height = displayHeight;
            this.fps = fps;
            this.transform = new MatrixTransform();
            brush.Transform = transform;
            loopStyle = LoopStyle.Linear;
        }

        public static void UpdateSpriteSheet(ImageBrush brush, int fps, LoopStyle? loopStyle = null)
        {
            SpriteSheetBehaviour? behaviour = behaviours.Find((x) => x.brush == brush);
            if (behaviour is not null)
            {
                behaviour.fps = fps;
                if (loopStyle != null)
                {
                    behaviour.loopStyle = (LoopStyle)loopStyle;
                }
            }
        }

        public static void SetSpriteSheet(ImageBrush brush, Image? image = null, int frames = 0, int columns = 0, int rows = 0, int fps = 0, int displayWidth = 0, int displayHeight = 0) {
            SpriteSheetBehaviour? behaviour = behaviours.Find((x) => x.brush == brush);
            if (behaviour is not null)
            {
                behaviours.Remove(behaviour);
                if (behaviours.Count == 0)
                {
                    CompositionTarget.Rendering -= OnUpdate;
                }
            }
            if (image is null)
            {
                brush.ImageSource = null;
                brush.Transform = null;
                return;
            }
            
            using (MemoryStream ms = new())
            {
                image.Save(ms, PngFormat.Instance);
                var imageSource = new BitmapImage();
                imageSource.BeginInit();
                imageSource.StreamSource = ms;
                imageSource.EndInit();
                imageSource.Freeze();
                brush.Stretch = Stretch.Fill;
                brush.AlignmentX = AlignmentX.Left;
                brush.AlignmentY = AlignmentY.Top;
                brush.ImageSource = imageSource; 
                behaviour = new SpriteSheetBehaviour(brush, frames, rows, columns, displayWidth, displayHeight, fps);
                behaviours.Add(behaviour);
            }
            if (behaviours.Count == 1)
            {
                CompositionTarget.Rendering += OnUpdate;
            }
        }

        private static void OnUpdate(object? sender, object e)
        {
            foreach (SpriteSheetBehaviour behaviour in behaviours) {
                DateTime now = DateTime.Now;
                TimeSpan ts = now - behaviour.instanceTime;
                int currentFrame = (int)(ts.TotalSeconds * (double)behaviour.fps) % (behaviour.loopStyle == LoopStyle.Linear ? behaviour.frames : (behaviour.frames * 2 - 2));
                if (behaviour.loopStyle == LoopStyle.PingPong && currentFrame >= behaviour.frames)
                {
                    currentFrame = (behaviour.frames * 2 - 2) - currentFrame;
                }
                var column = currentFrame % behaviour.columns;
                var row = currentFrame / behaviour.rows;
                Matrix transform = Matrix.Identity;
                transform.Scale(behaviour.columns, behaviour.rows);
                transform.Translate(-column * behaviour.width, -row * behaviour.height);
                behaviour.transform.Matrix = transform;
                if (ts.TotalSeconds > 10 && currentFrame == 0) {
                    behaviour.instanceTime = now;
                }
            }

            
        }
    }
}
