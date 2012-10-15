using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace SWCombine.SDK.Web
{
    [DataContract]
    public class RequestError
    {
        [DataMember(Name = "error")]
        public string Error { get; set; }
    }
}
