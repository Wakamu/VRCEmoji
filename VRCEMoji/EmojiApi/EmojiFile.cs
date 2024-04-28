using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using VRChat.API.Model;

namespace VRCEmoji.EmojiApi
{
    [DataContract(Name = "EmojiFile")]
    public partial class EmojiFile : IValidatableObject
    {

        [DataMember(Name = "mimeType", IsRequired = true, EmitDefaultValue = true)]
        public MIMEType MimeType { get; set; }

        [DataMember(Name = "frames", EmitDefaultValue = true)]
        public int Frames { get; set; }

        [JsonConstructorAttribute]
        protected EmojiFile() { }
        
        public EmojiFile(string extension = default(string), string id = default(string), MIMEType mimeType = default(MIMEType), string name = default(string), string ownerId = default(string), int frames = default(int))
        {
            if (extension == null)
            {
                throw new ArgumentNullException("extension is a required property for File and cannot be null");
            }
            this.Extension = extension;

            if (id == null)
            {
                throw new ArgumentNullException("id is a required property for File and cannot be null");
            }
            this.Id = id;
            this.MimeType = mimeType;
 
            if (name == null)
            {
                throw new ArgumentNullException("name is a required property for File and cannot be null");
            }
            this.Name = name;

            if (ownerId == null)
            {
                throw new ArgumentNullException("ownerId is a required property for File and cannot be null");
            }
            this.OwnerId = ownerId;
            this.Frames = frames;
        }

        
        [DataMember(Name = "extension", IsRequired = true, EmitDefaultValue = true)]
        public string Extension { get; set; }

        [DataMember(Name = "id", IsRequired = true, EmitDefaultValue = true)]
        public string Id { get; set; }

        [DataMember(Name = "name", IsRequired = true, EmitDefaultValue = true)]
        public string Name { get; set; }

        [DataMember(Name = "ownerId", IsRequired = true, EmitDefaultValue = true)]
        public string OwnerId { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("class File {\n");
            sb.Append("  Extension: ").Append(Extension).Append("\n");
            sb.Append("  Id: ").Append(Id).Append("\n");
            sb.Append("  MimeType: ").Append(MimeType).Append("\n");
            sb.Append("  Name: ").Append(Name).Append("\n");
            sb.Append("  OwnerId: ").Append(OwnerId).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        public virtual string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }

        public override bool Equals(object input)
        {
            return this.Equals(input as EmojiFile);
        }

        public bool Equals(EmojiFile input)
        {
            if (input == null)
            {
                return false;
            }
            return
                (
                    this.Extension == input.Extension ||
                    (this.Extension != null &&
                    this.Extension.Equals(input.Extension))
                ) &&
                (
                    this.Id == input.Id ||
                    (this.Id != null &&
                    this.Id.Equals(input.Id))
                ) &&
                (
                    this.MimeType == input.MimeType ||
                    this.MimeType.Equals(input.MimeType)
                ) &&
                (
                    this.Name == input.Name ||
                    (this.Name != null &&
                    this.Name.Equals(input.Name))
                ) &&
                (
                    this.OwnerId == input.OwnerId ||
                    (this.OwnerId != null &&
                    this.OwnerId.Equals(input.OwnerId))
                );
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 41;
                if (this.Extension != null)
                {
                    hashCode = (hashCode * 59) + this.Extension.GetHashCode();
                }
                if (this.Id != null)
                {
                    hashCode = (hashCode * 59) + this.Id.GetHashCode();
                }
                hashCode = (hashCode * 59) + this.MimeType.GetHashCode();
                if (this.Name != null)
                {
                    hashCode = (hashCode * 59) + this.Name.GetHashCode();
                }
                if (this.OwnerId != null)
                {
                    hashCode = (hashCode * 59) + this.OwnerId.GetHashCode();
                }
                return hashCode;
            }
        }

        public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(ValidationContext validationContext)
        {
            if (this.Extension != null && this.Extension.Length < 1)
            {
                yield return new System.ComponentModel.DataAnnotations.ValidationResult("Invalid value for Extension, length must be greater than 1.", new[] { "Extension" });
            }

            if (this.Name != null && this.Name.Length < 0)
            {
                yield return new System.ComponentModel.DataAnnotations.ValidationResult("Invalid value for Name, length must be greater than 0.", new[] { "Name" });
            }

            yield break;
        }
    }

}
