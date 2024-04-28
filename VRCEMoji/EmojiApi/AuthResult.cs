using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRChat.API.Model;

namespace VRCEMoji.EmojiApi
{
    internal class AuthResult
    {
        public bool Success { get; set; } = false;
        public VRChat.API.Client.Configuration? Configuration { get; set; }
        public CurrentUser? CurrentUser { get; set; }
        public string? ErrorMessage {  get; set; }
    }
}
