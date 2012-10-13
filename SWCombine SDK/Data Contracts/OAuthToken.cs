using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace SWCombine.SDK.OAuth
{
    [DataContract]
    public class OAuthToken
    {
        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        [DataMember(Name = "refresh_token")]
        public string RefreshToken { get; set; }

        [DataMember(Name = "expires_in")]
        public string ExpiresIn { get; set; }
    }
}
