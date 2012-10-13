using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace SWCombine.SDK.OAuth
{
    public enum AuthorisationResult
    {
        [Description("Unspecified Error")]
        Error = 0,

        [Description("Authorised")]
        Authorised,

        [Description("The end-user or authorization server denied the request")]
        Denied
    }
}
