using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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

        // Per-user, ACL-scoped. Previously we used CommonApplicationData
        // (C:\ProgramData\VRCEmoji) which is world-readable across local
        // accounts on the same machine — unsuitable for credentials.
        public static string StorageDir { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCEmoji");

        // Legacy world-readable location. Kept for one-shot migration of
        // existing users. Do not write here.
        public static string LegacyStorageDir { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VRCEmoji");

        // DPAPI-encrypted binary file containing the JSON-serialized StoredConfig.
        private static string AccountDatPath => Path.Combine(StorageDir, "account.dat");
        // Plaintext JSON file — legacy format, still read for one-shot migration.
        private static string AccountJsonPath => Path.Combine(StorageDir, "account.json");
        private static string LegacyAccountJsonPath => Path.Combine(LegacyStorageDir, "account.json");

        private StoredConfig? _storedConfig;

        private Configuration? _apiConfig;

        private static Authentication? _instance;

        public static Authentication Instance
        {
            get { return _instance ??= new Authentication(); }
        }

        private Authentication() {
            Directory.CreateDirectory(StorageDir);
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

        // DPAPI is Windows-only, CurrentUser scope. Ties the ciphertext to the
        // Windows account that wrote it — another local user cannot decrypt.
        private static byte[] Protect(string plaintext) =>
            ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), null, DataProtectionScope.CurrentUser);

        private static string? Unprotect(byte[] cipher)
        {
            try
            {
                return Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser));
            }
            catch (CryptographicException)
            {
                // Wrong user, corrupted file, or rebuilt profile. Force re-login.
                return null;
            }
        }

        private StoredConfig? ReadStoredConfig()
        {
            // 1. Preferred: DPAPI-encrypted account.dat at the per-user path.
            if (System.IO.File.Exists(AccountDatPath))
            {
                var json = Unprotect(System.IO.File.ReadAllBytes(AccountDatPath));
                if (json == null)
                {
                    // Corrupted / wrong user: remove the bad file so we don't loop.
                    try { System.IO.File.Delete(AccountDatPath); } catch { }
                    return null;
                }
                return JsonConvert.DeserializeObject<StoredConfig>(json);
            }

            // 2. Legacy plaintext JSON at the new per-user path OR the old
            //    world-readable path. Read whichever exists, then migrate to
            //    account.dat and delete the legacy file.
            string? legacyPath = null;
            if (System.IO.File.Exists(AccountJsonPath)) legacyPath = AccountJsonPath;
            else if (System.IO.File.Exists(LegacyAccountJsonPath)) legacyPath = LegacyAccountJsonPath;

            if (legacyPath == null) return null;

            var raw = System.IO.File.ReadAllText(legacyPath);
            var config = JsonConvert.DeserializeObject<StoredConfig>(raw);
            if (config == null) return null;

            // Migrate: write encrypted, delete legacy. Best-effort; if write
            // fails we leave the legacy file in place (no data loss).
            try
            {
                System.IO.File.WriteAllBytes(AccountDatPath, Protect(JsonConvert.SerializeObject(config)));
                try { System.IO.File.Delete(legacyPath); } catch { }
            }
            catch { /* keep legacy; next launch will retry */ }

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
            DeleteAllStoredFiles();
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
            DeleteAllStoredFiles();
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
            DeleteAllStoredFiles();
            StoredConfig storedConfig = new()
            {
                Auth = auth,
                TwoKey = twoKey,
                DisplayName = displayName
            };
            System.IO.File.WriteAllBytes(AccountDatPath, Protect(JsonConvert.SerializeObject(storedConfig)));
            this._storedConfig = storedConfig;
        }

        // Deletes every known storage file variant: the current DPAPI-encrypted
        // account.dat and both legacy plaintext account.json locations. Used on
        // login (clean slate) and logout.
        private static void DeleteAllStoredFiles()
        {
            try { System.IO.File.Delete(AccountDatPath); } catch { }
            try { System.IO.File.Delete(AccountJsonPath); } catch { }
            try { System.IO.File.Delete(LegacyAccountJsonPath); } catch { }
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
