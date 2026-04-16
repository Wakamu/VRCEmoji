using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using VRChat.API.Api;
using VRChat.API.Client;
using VRChat.API.Model;

namespace VRCEMoji.EmojiApi
{
    internal sealed class Authentication
    {
        private const string UserAgent = "VRCEmoji/1.2.0 wakamu";

        private static readonly JsonSerializerSettings ParseUserSettings = new()
        {
            Error = (sender, args) => { args.ErrorContext.Handled = true; }
        };
        private static readonly JsonSerializer ParseUserSerializer = JsonSerializer.Create(ParseUserSettings);

        public readonly string StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VRCEmoji");

        private StoredConfig? _storedConfig;

        private Configuration? _apiConfig;

        private static Authentication? _instance;

        public static Authentication Instance
        {
            get { return _instance ??= new Authentication(); }
        }

        private Authentication() {
            Directory.CreateDirectory(this.StoragePath);
            _storedConfig = ReadStoredConfig();
        }

        public Configuration? ApiConfig
        {
            get
            {
                if (_apiConfig is null && _storedConfig != null)
                {
                    Configuration config = new()
                    {
                        UserAgent = UserAgent
                    };
                    config.Username = _storedConfig.Username;
                    config.Password = _storedConfig.Password;
                    config.DefaultHeaders.Add("Cookie", "auth=" + _storedConfig.Auth + ";twoFactorAuth=" + _storedConfig.TwoKey);
                    _apiConfig = config;
                }
                return _apiConfig;
            }
        }

        public Configuration CreateConfig(string username, string password)
        {
            Configuration config = new()
            {
                UserAgent = UserAgent,
                Username = username,
                Password = password
            };
            _apiConfig = config;
            return config;
        }

        public void FinalizeAuth(ApiResponse<CurrentUser> currentUserResp, Configuration config)
        {
            var authCookie = currentUserResp.Cookies.Find(x => x.Name == "auth");
            var f2aCookie = currentUserResp.Cookies.Find(x => x.Name == "twoFactorAuth");
            if (authCookie != null && f2aCookie != null)
            {
                CreateStoredConfig(config, authCookie.Value, f2aCookie.Value,
                    ParseUser(currentUserResp)?.DisplayName ?? "null");
            }
        }

        public CurrentUser? ParseUser(ApiResponse<CurrentUser> currentUserResp)
        {
            var jObj = JObject.Parse(currentUserResp.RawContent);
            return jObj.ToObject<CurrentUser>(ParseUserSerializer);
        }

        public StoredConfig? StoredConfig
        {
            get {
                _storedConfig ??= ReadStoredConfig();
                return _storedConfig;
            }
        }

        private StoredConfig? ReadStoredConfig()
        {
            if (System.IO.File.Exists(this.StoragePath + "\\account.json"))
            {
                return JsonConvert.DeserializeObject<StoredConfig>(System.IO.File.ReadAllText(this.StoragePath + "\\account.json"));
            }
            return null;
        }

        public void LogOff()
        {
            System.IO.File.Delete(this.StoragePath + "\\account.json");
            this._storedConfig = null;
            this._apiConfig = null;
        }

        private void CreateStoredConfig(Configuration config, string auth, string twoKey, string displayName)
        {
            System.IO.File.Delete(this.StoragePath + "\\account.json");
            StoredConfig storedConfig = new()
            {
                Username = config.Username,
                Password = config.Password,
                Auth = auth,
                TwoKey = twoKey,
                DisplayName = displayName
            };
            System.IO.File.WriteAllText(this.StoragePath + "\\account.json", JsonConvert.SerializeObject(storedConfig));
            this._storedConfig = storedConfig;
        }

        public static bool RequiresEmail2FA(ApiResponse<CurrentUser> resp)
        {
            if (resp.RawContent.Contains("emailOtp"))
            {
                return true;
            }

            return false;
        }

        public static bool IsAuthed(ApiResponse<CurrentUser> resp)
        {
            if (resp.RawContent.Contains("displayName"))
            {
                return true;
            }

            return false;
        }
    }
}
