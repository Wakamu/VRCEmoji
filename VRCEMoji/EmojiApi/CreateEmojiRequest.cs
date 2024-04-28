using SixLabors.ImageSharp;
using System.Runtime.Serialization;
using VRChat.API.Model;

namespace VRCEMoji.EmojiApi
{
    public class CreateEmojiRequest : CreateFileRequest
    {
        [DataMember(Name = "animationStyle", IsRequired = true, EmitDefaultValue = true)]
        public AnimationStyle AnimationStyle { get; set; }
        
        [DataMember(Name = "loopStyle", IsRequired = true, EmitDefaultValue = true)]
        public LoopStyle LoopStyle { get; set; }

        [DataMember(Name = "maskTag", IsRequired = true, EmitDefaultValue = true)]
        public string MaskTag { get; set; } = "square";

        [DataMember(Name = "framesOverTime", IsRequired = true, EmitDefaultValue = true)]
        public int FPS { get; set; }

        [DataMember(Name = "frames", IsRequired = true, EmitDefaultValue = true)]
        public int Frames { get; set; }

        public string GetEnumMemberAttrValue<T>(T enumVal)
        {
            var enumType = typeof(T);
            var memInfo = enumType.GetMember(enumVal.ToString());
            var attr = memInfo.FirstOrDefault()?.GetCustomAttributes(false).OfType<EnumMemberAttribute>().FirstOrDefault();
            if (attr != null)
            {
                return attr.Value;
            }

            return null;
        }

        public CreateEmojiRequest(int frames, int fps, Image image) : base()
        {
            AnimationStyle = AnimationStyle.Aura;
            FPS = fps;
            Frames = frames;
            MimeType = MIMEType.ImagePng;
        }

        public Dictionary<string, string> GetFormParams()
        {
            var formParams = new Dictionary<string, string>();
            formParams.Add("tag", "emojianimated");
            formParams.Add("frames", this.Frames.ToString());
            formParams.Add("framesOverTime", this.FPS.ToString());
            formParams.Add("animationStyle", GetEnumMemberAttrValue(this.AnimationStyle));
            formParams.Add("maskTag", this.MaskTag.ToString());
            formParams.Add("loopStyle", GetEnumMemberAttrValue(this.LoopStyle));
            return formParams;
        }
    }
}
