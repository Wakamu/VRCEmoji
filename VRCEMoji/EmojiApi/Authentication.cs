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
                    // Username/Password are not persisted — cookies carry the session.
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
            var path = this.StoragePath + "\\account.json";
            if (!System.IO.File.Exists(path)) return null;

            var raw = System.IO.File.ReadAllText(path);
            var config = JsonConvert.DeserializeObject<StoredConfig>(raw);
            if (config == null) return null;

            // Proactive scrub: pre-upgrade files contained plaintext Username and
            // Password. Newtonsoft silently ignores unknown fields on deserialize,
            // so we detect them in the raw JSON and rewrite the file without them.
            if (raw.Contains("\"Password\"") || raw.Contains("\"Username\""))
            {
                try { System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(config)); }
                catch { /* best-effort; leaving plaintext is no worse than before */ }
            }
            return config;
        }

        /// <summary>
        /// Clears all local auth state: deletes the on-disk config file, scrubs
        /// the in-memory Configuration (cookies + credentials) so any captured
        /// reference becomes unusable, and nulls the singleton's caches.
        /// Never performs network IO — safe to call offline or after a rejected
        /// login.
        /// </summary>
        public void ClearLocal()
        {
            try { System.IO.File.Delete(this.StoragePath + "\\account.json"); } catch { }
            if (_apiConfig != null)
            {
                _apiConfig.DefaultHeaders.Remove("Cookie");
                _apiConfig.Username = "";
                _apiConfig.Password = "";
            }
            this._storedConfig = null;
            this._apiConfig = null;
        }

        /// <summary>
        /// Logs out locally and, best-effort, invalidates the server-side session.
        /// The file is deleted and the singleton's refs are cleared BEFORE the
        /// network call (so a crash mid-call cannot leave credentials on disk).
        /// The captured Configuration retains its cookies just long enough to
        /// make the server call, then gets scrubbed — neutralizing any external
        /// reference that was captured prior to logout. The server call is
        /// capped at 5s and swallows all errors (offline logout must still work).
        /// </summary>
        public async Task LogOffAsync()
        {
            var cfg = _apiConfig;
            try { System.IO.File.Delete(this.StoragePath + "\\account.json"); } catch { }
            _storedConfig = null;
            _apiConfig = null;

            if (cfg != null)
            {
                try
                {
                    var client = new CustomApiClient();
                    var authApi = new AuthenticationApi(client, client, cfg);
                    var logoutTask = Task.Run(() => authApi.Logout());
                    await Task.WhenAny(logoutTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
                }
                catch { /* best-effort; offline logout must still work */ }

                // Scrub the captured Configuration after the server call so any
                // external reference is neutralized even if it outlives us.
                try
                {
                    cfg.DefaultHeaders.Remove("Cookie");
                    cfg.Username = "";
                    cfg.Password = "";
                }
                catch { }
            }
        }

        private void CreateStoredConfig(Configuration config, string auth, string twoKey, string displayName)
        {
            System.IO.File.Delete(this.StoragePath + "\\account.json");
            StoredConfig storedConfig = new()
            {
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
