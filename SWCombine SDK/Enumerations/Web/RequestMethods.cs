using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace SWCombine.SDK.Web
{
    public static class RequestMethods
    {
        public const string Get = System.Net.WebRequestMethods.Http.Get;
        public const string Post = System.Net.WebRequestMethods.Http.Post;
        public const string Put = System.Net.WebRequestMethods.Http.Put;
        public const string Delete = "DELETE";
        public const string Head = System.Net.WebRequestMethods.Http.Head;
        public const string Option = "OPTION";
    }
}
