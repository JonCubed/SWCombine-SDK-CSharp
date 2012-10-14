using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SWCombine.SDK.Web;
using SWCombine.SDK.Extensions;
using System.Collections.Specialized;

namespace SWCombine.SDK.Web
{
    public class RequestCompleteEventArgs: System.EventArgs
    {
        #region Constructors

        public RequestCompleteEventArgs(NameValueCollection queryTokens)
        {
            this.QueryTokens = queryTokens;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The query string of the request.
        /// </summary>
        public NameValueCollection QueryTokens { get; private set; }
        
        #endregion
        
    }
}
