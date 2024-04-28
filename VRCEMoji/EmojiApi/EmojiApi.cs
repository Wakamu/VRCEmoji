using SixLabors.ImageSharp;
using System.IO;
using System.Net;
using VRCEmoji.EmojiApi;
using VRChat.API.Api;

namespace VRCEMoji.EmojiApi
{
    public class EmojiApi: FilesApi
    {
        public EmojiApi(VRChat.API.Client.ISynchronousClient client, VRChat.API.Client.IAsynchronousClient asyncClient, VRChat.API.Client.IReadableConfiguration configuration) : base(client, asyncClient, configuration)
        {
        }

        public EmojiFile CreateEmoji(Image image, string name, CreateEmojiRequest createFileRequest = default(CreateEmojiRequest), int operationIndex = 0)
        {
            VRChat.API.Client.ApiResponse<EmojiFile> localVarResponse = CreateEmojiFileWithHttpInfo(image, name, createFileRequest);
            return localVarResponse.Data;
        }

        public VRChat.API.Client.ApiResponse<EmojiFile> CreateEmojiFileWithHttpInfo(Image image ,string name, CreateEmojiRequest createFileRequest = default(CreateEmojiRequest), int operationIndex = 0)
        {
            VRChat.API.Client.RequestOptions localVarRequestOptions = new VRChat.API.Client.RequestOptions();

            localVarRequestOptions.HeaderParameters.Add("Accept", "*/*");

            localVarRequestOptions.FormParameters = createFileRequest.GetFormParams();
            localVarRequestOptions.FileParameters = new VRChat.API.Client.Multimap<string, System.IO.Stream>();
            using (Stream st = new MemoryStream())
            {
                image.SaveAsPng(st);
                st.Position = 0;
                localVarRequestOptions.FileParameters.Add(name, st);
                localVarRequestOptions.Operation = "FilesApi.CreateFile";
                localVarRequestOptions.OperationIndex = operationIndex;
                if (!string.IsNullOrEmpty(this.Configuration.GetApiKeyWithPrefix("auth")))
                {
                    localVarRequestOptions.Cookies.Add(new Cookie("auth", this.Configuration.GetApiKeyWithPrefix("auth")));
                }
                var localVarResponse = ((CustomApiClient)(this.Client)).PostEmoji<EmojiFile>("/file/image", localVarRequestOptions, this.Configuration);
                if (this.ExceptionFactory != null)
                {
                    Exception _exception = this.ExceptionFactory("CreateFile", localVarResponse);
                    if (_exception != null)
                    {
                        throw _exception;
                    }
                }
                return localVarResponse;
            }
        }

        public List<EmojiFile> GetEmojiFiles(string userId = default(string), int? n = default(int?), int? offset = default(int?), int operationIndex = 0)
        {
            VRChat.API.Client.ApiResponse<List<EmojiFile>> localVarResponse = GetEmojiFilesWithHttpInfo(userId, n, offset);
            return localVarResponse.Data;
        }

        public VRChat.API.Client.ApiResponse<List<EmojiFile>> GetEmojiFilesWithHttpInfo(string userId = default(string), int? n = default(int?), int? offset = default(int?), int operationIndex = 0)
        {
            VRChat.API.Client.RequestOptions localVarRequestOptions = new VRChat.API.Client.RequestOptions();

            string[] _contentTypes = new string[] {
            };

            string[] _accepts = new string[] {
                "application/json"
            };

            var localVarContentType = VRChat.API.Client.ClientUtils.SelectHeaderContentType(_contentTypes);
            if (localVarContentType != null)
            {
                localVarRequestOptions.HeaderParameters.Add("Content-Type", localVarContentType);
            }

            var localVarAccept = VRChat.API.Client.ClientUtils.SelectHeaderAccept(_accepts);
            if (localVarAccept != null)
            {
                localVarRequestOptions.HeaderParameters.Add("Accept", localVarAccept);
            }
            if (userId != null)
            {
                localVarRequestOptions.QueryParameters.Add(VRChat.API.Client.ClientUtils.ParameterToMultiMap("", "userId", userId));
            }
            if (n != null)
            {
                localVarRequestOptions.QueryParameters.Add(VRChat.API.Client.ClientUtils.ParameterToMultiMap("", "n", n));
            }
            if (offset != null)
            {
                localVarRequestOptions.QueryParameters.Add(VRChat.API.Client.ClientUtils.ParameterToMultiMap("", "offset", offset));
            }
            localVarRequestOptions.QueryParameters.Add(VRChat.API.Client.ClientUtils.ParameterToMultiMap("", "tag", "emoji"));

            localVarRequestOptions.Operation = "FilesApi.GetFiles";
            localVarRequestOptions.OperationIndex = operationIndex;
            if (!string.IsNullOrEmpty(this.Configuration.GetApiKeyWithPrefix("auth")))
            {
                localVarRequestOptions.Cookies.Add(new Cookie("auth", this.Configuration.GetApiKeyWithPrefix("auth")));
            }
            var localVarResponse = this.Client.Get<List<EmojiFile>>("/files", localVarRequestOptions, this.Configuration);
            if (this.ExceptionFactory != null)
            {
                Exception _exception = this.ExceptionFactory("GetFiles", localVarResponse);
                if (_exception != null)
                {
                    throw _exception;
                }
            }

            return localVarResponse;
        }
    }
}
