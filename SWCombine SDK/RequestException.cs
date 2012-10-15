using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SWCombine.SDK.Web;
using System.Net;

namespace SWCombine.SDK
{
    class RequestException : Exception
    {
        public RequestError RequestError { get; private set; }
        public HttpStatusCode StatusCode { get; private set; }
        public string StatusDescription { get; private set; }

        public RequestException(RequestError error, HttpStatusCode statusCode, string statusDescription)
        {
            this.RequestError = error;
            this.StatusCode = statusCode;
            this.StatusDescription = statusDescription;
        }

        public RequestException(RequestError error, WebException ex) : base(error.Error, ex)
        {
            var response = ex.Response as HttpWebResponse;
            this.RequestError = error;
            this.StatusCode = response.StatusCode;
            this.StatusDescription = response.StatusDescription;
        }
    }
}
