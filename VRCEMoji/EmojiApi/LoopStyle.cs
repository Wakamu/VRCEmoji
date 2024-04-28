using System.Runtime.Serialization;

namespace VRCEMoji.EmojiApi
{
    public enum LoopStyle
    {
        [EnumMember(Value = "linear")]
        Linear = 1,

        [EnumMember(Value = "pingpong")]
        PingPong = 2,
    }
}
