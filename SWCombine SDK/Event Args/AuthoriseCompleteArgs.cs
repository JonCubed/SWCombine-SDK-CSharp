using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SWCombine.SDK.Web;
using SWCombine.SDK.Extensions;

namespace SWCombine.SDK.OAuth
{
    public class AuthoriseCompleteArgs: System.EventArgs
    {

        #region Members

        private AuthorisationResult _result;
        private string _reason;

        #endregion

        #region Constructors

        public AuthoriseCompleteArgs(AuthorisationResult result, string state) : this(result, result.GetDescription(), state) { }

        public AuthoriseCompleteArgs(AuthorisationResult result, string deniedReason, string state)
        {
            _result = result;
            _reason = String.IsNullOrWhiteSpace(deniedReason) ? result.GetDescription() : deniedReason;
            this.State = state;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Whether the user has authorised the app.
        /// </summary>
        public AuthorisationResult Result
        {
            get { return _result; }
        }

        /// <summary>
        /// Reason app has not been authorised.
        /// </summary>
        public string DeniedReason
        {
            get { return _reason; }
        }

        public string State { get; private set; }

        #endregion
        
    }
}
