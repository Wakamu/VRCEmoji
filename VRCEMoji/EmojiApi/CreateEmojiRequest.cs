using SixLabors.ImageSharp;
using System.Runtime.Serialization;
using VRCEMoji.EmojiGeneration;
using VRChat.API.Model;

namespace VRCEMoji.EmojiApi
{
    public class CreateEmojiRequest : CreateFileRequest
    {
        [DataMember(Name = "animationStyle", IsRequired = true, EmitDefaultValue = true)]
        public AnimationStyle AnimationStyle { get; set; }
        
        [DataMember(Name = "loopStyle", IsRequired = true, EmitDefaultValue = true)]
        public LoopStyle LoopStyle { get; set; }

        [DataMember(Name = "image", IsRequired = true, EmitDefaultValue = true)]
        public Image Image { get; set; }

        [DataMember(Name = "maskTag", IsRequired = true, EmitDefaultValue = true)]
        public string MaskTag { get; set; } = "square";

        [DataMember(Name = "framesOverTime", IsRequired = true, EmitDefaultValue = true)]
        public int FPS { get; set; }

        [DataMember(Name = "frames", IsRequired = true, EmitDefaultValue = true)]
        public int Frames { get; set; }

        public string Tag { get; set; }

        public CreateEmojiRequest(GenerationResult generationResult, UploadSettings settings) : base()
        {
            AnimationStyle = settings.AnimationStyle;
            LoopStyle = settings.LoopStyle;
            FPS = settings.FPSOverride > 0 ? settings.FPSOverride : generationResult.FPS;
            Frames = generationResult.Frames;
            MimeType = MIMEType.ImagePng;
            Image = generationResult.Image;
            Name = generationResult.Name + "_" + generationResult.Frames + "frames_" + FPS + "fps.png";
            Extension = ".png";
            Tag = generationResult.GenerationType == GenerationType.Emoji ? (generationResult.Frames > 1 ? "emojianimated" : "emoji") : "sticker";
        }

        public Dictionary<string, string> GetFormParams()
        {
            if (this.Tag == "sticker")
            {
                var formParams = new Dictionary<string, string>
                {
                    { "tag", this.Tag },
                    { "maskTag", this.MaskTag.ToString() }
                };
                return formParams;
            }
            else
            {
                var formParams = new Dictionary<string, string>
                {
                    { "tag", this.Tag },
                    { "frames", this.Frames.ToString() },
                    { "framesOverTime", this.FPS.ToString() },
                    { "animationStyle", EnumHelper.GetMemberValue(this.AnimationStyle) ?? "aura" },
                    { "maskTag", this.MaskTag.ToString() },
                    { "loopStyle", EnumHelper.GetMemberValue(this.LoopStyle) ?? "linear" }
                };
                return formParams;
            }
        }
    }
}
