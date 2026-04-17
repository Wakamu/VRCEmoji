using System.Runtime.Serialization;

namespace VRCEMoji.EmojiApi
{
    public static class EnumHelper
    {
        public static string? GetMemberValue<T>(T enumVal) where T : struct, Enum
        {
            var memInfo = typeof(T).GetMember(enumVal.ToString());
            var attr = memInfo.FirstOrDefault()?.GetCustomAttributes(false).OfType<EnumMemberAttribute>().FirstOrDefault();
            return attr?.Value;
        }

        public static T? FindByMemberValue<T>(string? value) where T : struct, Enum
        {
            if (value == null) return null;
            foreach (T enumVal in Enum.GetValues(typeof(T)).Cast<T>())
            {
                if (GetMemberValue(enumVal) == value)
                    return enumVal;
            }
            return null;
        }
    }
}
