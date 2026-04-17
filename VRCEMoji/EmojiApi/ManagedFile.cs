using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace VRCEMoji.EmojiApi
{
    [DataContract(Name = "ManagedFile")]
    public class ManagedFile
    {
        [DataMember(Name = "id", EmitDefaultValue = true)]
        public string Id { get; set; } = "";

        [DataMember(Name = "name", EmitDefaultValue = true)]
        public string Name { get; set; } = "";

        [DataMember(Name = "ownerId", EmitDefaultValue = true)]
        public string OwnerId { get; set; } = "";

        [DataMember(Name = "tags", EmitDefaultValue = true)]
        public List<string> Tags { get; set; } = [];

        [DataMember(Name = "animationStyle", EmitDefaultValue = true)]
        public string? AnimationStyle { get; set; }

        [DataMember(Name = "loopStyle", EmitDefaultValue = true)]
        public string? LoopStyle { get; set; }

        [DataMember(Name = "frames", EmitDefaultValue = true)]
        public int Frames { get; set; }

        [DataMember(Name = "framesOverTime", EmitDefaultValue = true)]
        public int FramesOverTime { get; set; }

        [DataMember(Name = "maskTag", EmitDefaultValue = true)]
        public string? MaskTag { get; set; }

        [DataMember(Name = "extension", EmitDefaultValue = true)]
        public string Extension { get; set; } = "";

        [DataMember(Name = "mimeType", EmitDefaultValue = true)]
        public string MimeType { get; set; } = "";

        [JsonIgnore]
        public bool IsEmoji => Tags.Contains("emoji") || Tags.Contains("emojianimated");

        [JsonIgnore]
        public bool IsSticker => Tags.Contains("sticker");

        [JsonIgnore]
        public bool IsAnimated => Tags.Contains("emojianimated");

        /// <summary>
        /// Best-effort frame count: uses API field if available, otherwise parses
        /// the filename for the "Xframes" pattern set by this app during upload.
        /// Returns 0 if neither source provides a count.
        /// </summary>
        [JsonIgnore]
        public int DetectedFrames
        {
            get
            {
                if (Frames > 0) return Frames;
                var match = Regex.Match(Name, @"(\d+)frames", RegexOptions.IgnoreCase);
                return match.Success ? int.Parse(match.Groups[1].Value) : 0;
            }
        }

        /// <summary>
        /// Best-effort FPS: uses API field if available, otherwise parses
        /// the filename for the "Xfps" pattern. Defaults to 8 if unknown.
        /// </summary>
        [JsonIgnore]
        public int DetectedFPS
        {
            get
            {
                if (FramesOverTime > 0) return FramesOverTime;
                var match = Regex.Match(Name, @"(\d+)fps", RegexOptions.IgnoreCase);
                return match.Success ? int.Parse(match.Groups[1].Value) : 8;
            }
        }

        /// <summary>
        /// Returns the URL for the latest version of this file's image.
        /// VRChat file URLs follow the pattern: /file/{id}/{version}/file
        /// Version 1 is always the initial upload; the app deletes and recreates files
        /// rather than uploading new versions, so version 1 is always current.
        /// </summary>
        [JsonIgnore]
        public string ImageUrl => $"https://api.vrchat.cloud/api/1/file/{Id}/1/file";
    }
}
