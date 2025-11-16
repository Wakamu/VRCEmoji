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
                if (_apiConfig is null)
                {
                    Configuration config = new()
                    {
                        UserAgent = "VRCEmoji/1.2.0 wakamu"
                    };
                    if (this._storedConfig != null)
                    {
                        config.Username = _storedConfig.Username;
                        config.Password = _storedConfig.Password;
                        config.DefaultHeaders.Add("Cookie", "auth=" + _storedConfig.Auth + ";twoFactorAuth=" + _storedConfig.TwoKey);
                        _apiConfig = config;
                    }
                    else
                    {
                        LoginDialog loginDialog = new() { Owner = MainWindow.Instance };
                        if (loginDialog.ShowDialog() == true)
                        {
                            config.Username = loginDialog.Login;
                            config.Password = loginDialog.Password;
                            _apiConfig = config;
                        }
                    }
                    
                }
                return _apiConfig;
            }
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

        public AuthResult HandleAuth()
        {
            Configuration? config = ApiConfig;
            AuthResult result = new();
            if (config == null)
            {
                return result;
            }
            bool logged = false;
            CustomApiClient client = new();
            AuthenticationApi authApi = new(client, client, config);
            try
            {
                ApiResponse<CurrentUser> currentUserResp = authApi.GetCurrentUserWithHttpInfo();
                bool cancelOperation = false;
                logged = true;
                if (!IsAuthed(currentUserResp) && !cancelOperation)
                {
                    if (RequiresEmail2FA(currentUserResp))
                    {
                        InputDialog inputDialog = new("Please verify with the OTP code sent to your email.")
                        {
                            Owner = MainWindow.Instance
                        };
                        if (inputDialog.ShowDialog() == true)
                        {
                            authApi.Verify2FAEmailCode(new TwoFactorEmailCode(inputDialog.Answer));
                            currentUserResp = authApi.GetCurrentUserWithHttpInfo();
                        }
                        else
                        {
                            cancelOperation = true;
                        }
                    }
                    else
                    {
                        InputDialog inputDialog = new("Please verify with your double authentication code.")
                        {
                            Owner = MainWindow.Instance
                        };
                        if (inputDialog.ShowDialog() == true)
                        {
                            authApi.Verify2FA(new TwoFactorAuthCode(inputDialog.Answer));
                            currentUserResp = authApi.GetCurrentUserWithHttpInfo();
                        }
                        else { cancelOperation = true; }
                    }
                }
                if (cancelOperation)
                {
                    return result;
                }
                var authCookie = currentUserResp.Cookies.Find(x => x.Name == "auth");
                var f2aCookie = currentUserResp.Cookies.Find(x => x.Name == "twoFactorAuth");
                var settings = new JsonSerializerSettings
                {
                    Error = (sender, args) =>
                    {
                        args.ErrorContext.Handled = true;
                    }
                };
                var serializer = JsonSerializer.Create(settings);
                var jObj = JObject.Parse(currentUserResp.RawContent);
                CurrentUser? user = jObj.ToObject<CurrentUser>(serializer);
                if (user is null) {
                    return result;
                }
                if (authCookie != null && f2aCookie != null)
                {
                    var auth = authCookie.Value;
                    var f2a = f2aCookie.Value;
                    CreateStoredConfig(config, auth, f2a, user.DisplayName ?? "null");
                }
                result.Success = true;
                result.CurrentUser = user;
                result.Configuration = config;
                return result;
            }
            catch (ApiException)
            {
                result.ErrorMessage = logged ? "An error occured with the two factor authentication." : "Invalid username/password.";
                return result;
            }
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

        static bool RequiresEmail2FA(ApiResponse<CurrentUser> resp)
        {
            if (resp.RawContent.Contains("emailOtp"))
            {
                return true;
            }

            return false;
        }

        static bool IsAuthed(ApiResponse<CurrentUser> resp)
        {
            if (resp.RawContent.Contains("displayName"))
            {
                return true;
            }

            return false;
        }
    }
}
