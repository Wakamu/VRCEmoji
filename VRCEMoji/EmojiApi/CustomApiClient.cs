using RestSharp;
using System.IO;
using VRChat.API.Client;
using RestSharp.Serializers;
using RestSharpMethod = RestSharp.Method;
using Polly;
using System.Net;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace VRCEMoji.EmojiApi
{
    internal class CustomJsonCodec : IRestSerializer, ISerializer, IDeserializer
    {
        private readonly IReadableConfiguration _configuration;
        private static readonly string _contentType = "application/json";
        private readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    OverrideSpecifiedNames = false
                }
            }
        };

        public CustomJsonCodec(IReadableConfiguration configuration)
        {
            _configuration = configuration;
        }

        public CustomJsonCodec(JsonSerializerSettings serializerSettings, IReadableConfiguration configuration)
        {
            _serializerSettings = serializerSettings;
            _configuration = configuration;
        }

        public string Serialize(object obj)
        {
            if (obj != null && obj is VRChat.API.Model.AbstractOpenAPISchema)
            {
                return ((VRChat.API.Model.AbstractOpenAPISchema)obj).ToJson();
            }
            else
            {
                return JsonConvert.SerializeObject(obj, _serializerSettings);
            }
        }

        public string Serialize(Parameter bodyParameter) => Serialize(bodyParameter.Value);

        public T Deserialize<T>(RestResponse response)
        {
            var result = (T)Deserialize(response, typeof(T));
            return result;
        }

        internal object Deserialize(RestResponse response, Type type)
        {
            if (type == typeof(byte[]))
            {
                return response.RawBytes;
            }

            if (type == typeof(Stream))
            {
                var bytes = response.RawBytes;
                if (response.Headers != null)
                {
                    var filePath = string.IsNullOrEmpty(_configuration.TempFolderPath)
                        ? Path.GetTempPath()
                        : _configuration.TempFolderPath;
                    var regex = new Regex(@"Content-Disposition=.*filename=['""]?([^'""\s]+)['""]?$");
                    foreach (var header in response.Headers)
                    {
                        var match = regex.Match(header.ToString());
                        if (match.Success)
                        {
                            string fileName = filePath + ClientUtils.SanitizeFilename(match.Groups[1].Value.Replace("\"", "").Replace("'", ""));
                            File.WriteAllBytes(fileName, bytes);
                            return new FileStream(fileName, FileMode.Open);
                        }
                    }
                }
                var stream = new MemoryStream(bytes);
                return stream;
            }

            if (type.Name.StartsWith("System.Nullable`1[[System.DateTime"))
            {
                return DateTime.Parse(response.Content, null, System.Globalization.DateTimeStyles.RoundtripKind);
            }

            if (type == typeof(string) || type.Name.StartsWith("System.Nullable"))
            {
                return Convert.ChangeType(response.Content, type);
            }

            try
            {
                return JsonConvert.DeserializeObject(response.Content, type, _serializerSettings);
            }
            catch (Exception e)
            {
                throw new ApiException(500, e.Message);
            }
        }

        public ISerializer Serializer => this;
        public IDeserializer Deserializer => this;

        public string[] AcceptedContentTypes => RestSharp.Serializers.ContentType.JsonAccept;

        public SupportsContentType SupportsContentType => contentType =>
            contentType.EndsWith("json", StringComparison.InvariantCultureIgnoreCase) ||
            contentType.EndsWith("javascript", StringComparison.InvariantCultureIgnoreCase);

        public string ContentType
        {
            get { return _contentType; }
            set { throw new InvalidOperationException("Not allowed to set content type."); }
        }

        public DataFormat DataFormat => DataFormat.Json;
    }

    public partial class CustomApiClient: ApiClient
    {
        private readonly string _baseUrl;

        public CustomApiClient()
        {
            _baseUrl = VRChat.API.Client.GlobalConfiguration.Instance.BasePath;
        }
        private RestSharpMethod Method(HttpMethod method)
        {
            RestSharpMethod other;
            switch (method)
            {
                case HttpMethod.Get:
                    other = RestSharpMethod.Get;
                    break;
                case HttpMethod.Post:
                    other = RestSharpMethod.Post;
                    break;
                case HttpMethod.Put:
                    other = RestSharpMethod.Put;
                    break;
                case HttpMethod.Delete:
                    other = RestSharpMethod.Delete;
                    break;
                case HttpMethod.Head:
                    other = RestSharpMethod.Head;
                    break;
                case HttpMethod.Options:
                    other = RestSharpMethod.Options;
                    break;
                case HttpMethod.Patch:
                    other = RestSharpMethod.Patch;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("method", method, null);
            }

            return other;
        }

        public ApiResponse<T> PostEmoji<T>(string path, RequestOptions options, IReadableConfiguration configuration = null)
        {
            var config = configuration ?? GlobalConfiguration.Instance;
            return Exec<T>(NewRequest(HttpMethod.Post, path, options, config), options, config);
        }

        private ApiResponse<T> Exec<T>(RestRequest req, RequestOptions options, IReadableConfiguration configuration)
        {
            var baseUrl = configuration.GetOperationServerUrl(options.Operation, options.OperationIndex) ?? _baseUrl;

            var cookies = CookieContainer;

            if (options.Cookies != null && options.Cookies.Count > 0)
            {
                foreach (var cookie in options.Cookies)
                {
                    cookies.Add(new Cookie(cookie.Name, cookie.Value));
                }
            }

            var clientOptions = new RestClientOptions(baseUrl)
            {
                ClientCertificates = configuration.ClientCertificates,
                CookieContainer = cookies,
                MaxTimeout = configuration.Timeout,
                Proxy = configuration.Proxy,
                UserAgent = configuration.UserAgent
            };

            RestClient client = new RestClient(clientOptions)
                .UseSerializer(() => new CustomJsonCodec(SerializerSettings, configuration));

            InterceptRequest(req);

            RestResponse<T> response;
            if (RetryConfiguration.RetryPolicy != null)
            {
                var policy = RetryConfiguration.RetryPolicy;
                var policyResult = policy.ExecuteAndCapture(() => client.Execute(req));
                response = (policyResult.Outcome == OutcomeType.Successful) ? client.Deserialize<T>(policyResult.Result) : new RestResponse<T>
                {
                    Request = req,
                    ErrorException = policyResult.FinalException
                };
            }
            else
            {
                response = client.Execute<T>(req);
            }

            if (typeof(VRChat.API.Model.AbstractOpenAPISchema).IsAssignableFrom(typeof(T)))
            {
                try
                {
                    response.Data = (T)typeof(T).GetMethod("FromJson").Invoke(null, new object[] { response.Content });
                }
                catch (Exception ex)
                {
                    throw ex.InnerException != null ? ex.InnerException : ex;
                }
            }
            else if (typeof(T).Name == "Stream")
            {
                response.Data = (T)(object)new MemoryStream(response.RawBytes);
            }
            else if (typeof(T).Name == "Byte[]")
            {
                response.Data = (T)(object)response.RawBytes;
            }
            else if (typeof(T).Name == "String")
            {
                response.Data = (T)(object)response.Content;
            }

            InterceptResponse(req, response);

            var result = ToApiResponse(response);
            if (response.ErrorMessage != null)
            {
                result.ErrorText = response.ErrorMessage;
            }

            if (response.Cookies != null && response.Cookies.Count > 0)
            {
                if (result.Cookies == null) result.Cookies = new List<Cookie>();
                foreach (var restResponseCookie in response.Cookies.Cast<Cookie>())
                {
                    var cookie = new Cookie(
                        restResponseCookie.Name,
                        restResponseCookie.Value,
                        restResponseCookie.Path,
                        restResponseCookie.Domain
                    )
                    {
                        Comment = restResponseCookie.Comment,
                        CommentUri = restResponseCookie.CommentUri,
                        Discard = restResponseCookie.Discard,
                        Expired = restResponseCookie.Expired,
                        Expires = restResponseCookie.Expires,
                        HttpOnly = restResponseCookie.HttpOnly,
                        Port = restResponseCookie.Port,
                        Secure = restResponseCookie.Secure,
                        Version = restResponseCookie.Version
                    };

                    result.Cookies.Add(cookie);
                    client.CookieContainer.Add(cookie);
                }
            }
            return result;
        }

        private ApiResponse<T> ToApiResponse<T>(RestResponse<T> response)
        {
            T result = response.Data;
            string rawContent = response.Content;

            var transformed = new ApiResponse<T>(response.StatusCode, new Multimap<string, string>(), result, rawContent)
            {
                ErrorText = response.ErrorMessage,
                Cookies = new List<Cookie>()
            };

            if (response.Headers != null)
            {
                foreach (var responseHeader in response.Headers)
                {
                    transformed.Headers.Add(responseHeader.Name, ClientUtils.ParameterToString(responseHeader.Value));
                }
            }

            if (response.ContentHeaders != null)
            {
                foreach (var responseHeader in response.ContentHeaders)
                {
                    transformed.Headers.Add(responseHeader.Name, ClientUtils.ParameterToString(responseHeader.Value));
                }
            }

            if (response.Cookies != null)
            {
                foreach (var responseCookies in response.Cookies.Cast<Cookie>())
                {
                    transformed.Cookies.Add(
                        new Cookie(
                            responseCookies.Name,
                            responseCookies.Value,
                            responseCookies.Path,
                            responseCookies.Domain)
                        );
                }
            }

            return transformed;
        }

        partial void InterceptResponse(RestRequest request, RestResponse response);

        partial void InterceptRequest(RestRequest request);

        private RestRequest NewRequest(
            HttpMethod method,
            string path,
            RequestOptions options,
            IReadableConfiguration configuration)
        {
            if (path == null) throw new ArgumentNullException("path");
            if (options == null) throw new ArgumentNullException("options");
            if (configuration == null) throw new ArgumentNullException("configuration");

            RestRequest request = new RestRequest(path, Method(method));

            if (options.PathParameters != null)
            {
                foreach (var pathParam in options.PathParameters)
                {
                    request.AddParameter(pathParam.Key, pathParam.Value, ParameterType.UrlSegment);
                }
            }

            if (options.QueryParameters != null)
            {
                foreach (var queryParam in options.QueryParameters)
                {
                    foreach (var value in queryParam.Value)
                    {
                        request.AddQueryParameter(queryParam.Key, value);
                    }
                }
            }

            if (configuration.DefaultHeaders != null)
            {
                foreach (var headerParam in configuration.DefaultHeaders)
                {
                    request.AddHeader(headerParam.Key, headerParam.Value);
                }
            }

            if (options.HeaderParameters != null)
            {
                foreach (var headerParam in options.HeaderParameters)
                {
                    foreach (var value in headerParam.Value)
                    {
                        request.AddHeader(headerParam.Key, value);
                    }
                }
            }

            if (options.FormParameters != null)
            {
                foreach (var formParam in options.FormParameters)
                {
                    request.AddParameter(formParam.Key, formParam.Value);
                }
            }

            if (options.Data != null)
            {
                if (options.Data is Stream stream)
                {
                    var contentType = "application/octet-stream";
                    if (options.HeaderParameters != null)
                    {
                        var contentTypes = options.HeaderParameters["Content-Type"];
                        contentType = contentTypes[0];
                    }

                    var bytes = ClientUtils.ReadAsBytes(stream);
                    request.AddParameter(contentType, bytes, ParameterType.RequestBody);
                }
                else
                {
                    if (options.HeaderParameters != null)
                    {
                        var contentTypes = options.HeaderParameters["Content-Type"];
                        if (contentTypes == null || contentTypes.Any(header => header.Contains("application/json")))
                        {
                            request.RequestFormat = DataFormat.Json;
                        }
                        else
                        {
                            
                        }
                    }
                    else
                    {
                        request.RequestFormat = DataFormat.Json;
                    }

                    request.AddJsonBody(options.Data);
                }
            }

            if (options.FileParameters != null)
            {
                foreach (var fileParam in options.FileParameters)
                {
                    foreach (var file in fileParam.Value)
                    {
                        var bytes = ClientUtils.ReadAsBytes(file);
                        request.AddFile("file", bytes, fileParam.Key, "image/png");
                    }
                }
            }

            return request;
        }
    }
}
