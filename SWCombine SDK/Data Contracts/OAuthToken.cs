using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System;

namespace SWCombine.SDK.OAuth
{
    /// <summary>
    /// Represents OAuth 2.0 Tokens
    /// </summary>
    [DataContract]
    public class OAuthToken
    {
        #region Members

        private int _expiresIn;
        private DateTime _expiresAt;

        #endregion

        #region Properties

        /// <summary>
        /// Access token used when making a request.
        /// </summary>
        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        /// <summary>
        /// Refresh token used to get renewed Access token.
        /// </summary>
        [DataMember(Name = "refresh_token")]
        public string RefreshToken { get; set; }

        /// <summary>
        /// Total remaining life of access token in seconds .
        /// </summary>
        [DataMember(Name = "expires_in")]
        public int ExpiresIn
        {
            get
            {
                return _expiresIn;
            }
            set
            {
                _expiresAt = DateTime.Now.AddSeconds(value);
                _expiresIn = value;
            }
        }

        /// <summary>
        /// Date and time when access token will expire.
        /// </summary>
        public DateTime ExpiresAt
        {
            get
            {
                return _expiresAt;
            }
        }

        #endregion
        
    }
}
