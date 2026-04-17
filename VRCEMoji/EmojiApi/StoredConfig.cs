namespace VRCEMoji.EmojiApi
{
    // Only the cookies + display name are persisted. The password is never
    // stored — cookies alone are sufficient to restore the session. Username
    // is also omitted (not needed post-login; identity flows via cookies).
    internal class StoredConfig
    {
        public string Auth { get; set; } = "";
        public string TwoKey { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
}
