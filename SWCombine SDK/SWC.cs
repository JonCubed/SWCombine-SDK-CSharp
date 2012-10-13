using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Specialized;
using SWCombine.SDK.OAuth;
using System.Net;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using SWCombine.SDK.Web;
using System.Web;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;

namespace SWCombine.SDK
{
    public delegate void AuthoriseCompleteHandler(object sender, AuthoriseCompleteArgs e);

    public class SWC
    {
        private const string Out_Of_Band_Uri = "urn:ietf:wg:oauth:2.0:oob";
        private Form _webBrowserForm;
        private static bool _wasAutoClosed = false;

        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUri { get; private set; }
        public string Character { get; private set; }
        public OAuthToken Token { get; private set; }
        private string Cookie { get; set; }
        public bool Shared { get; private set; }

        public event AuthoriseCompleteHandler AuthoriseComplete;

        protected virtual void OnAuthoriseComplete(AuthoriseCompleteArgs e)
        {
            if (AuthoriseComplete != null)
            {
                AuthoriseComplete(this, e);
            }            
        }

        /// <summary>
        /// Initialise a SWC object with client id and secret
        /// </summary>
        /// <param name="clientId">Client id for the app.</param>
        /// <param name="clientSecret">Client secret for the app.</param>
        /// <param name="port">Port number to listen to on localhost for authorisation responses.</param>
        /// <param name="shared">If true then app is on shared machine and persistent data will not be saved.</param>
        /// <returns>Returns an initialised SWC object</returns>
        public static SWC Initialise(string clientId, string clientSecret, int? port = null, bool shared = false)
        {
            var swc = new SWC() 
                        {
                            ClientId = clientId
                            ,ClientSecret = clientSecret
                            ,RedirectUri = port.HasValue ? new UriBuilder("http", "localhost", port.Value).ToString() : Out_Of_Band_Uri
                            ,Shared = shared
                        };

            if (!shared)
            {
                // load persistent data
                var data = PersistentData.Load();

                if (!string.IsNullOrEmpty(data.RefreshToken))
                {
                    swc.Cookie = data.Cookie;
                }

                if (!string.IsNullOrEmpty(data.RefreshToken))
                {
                    swc.Token = new OAuthToken() { RefreshToken = data.RefreshToken };
                }
            }            

            return swc;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scopes"></param>
        /// <param name="state"></param>
        public void AttemptAuthorise(List<string> scopes, string state)
        {
            if (_webBrowserForm != null)
            {
                throw new ApplicationException("Login in progress, must explicitly abort current login.");
            }

            var url = String.Format("{0}?response_type=code&client_id={1}&scope={2}&redirect_uri={3}&state={4}",
                                    OAuthEnpoints.Auth, 
                                    this.ClientId, 
                                    string.Join(" ", scopes),
                                    this.RedirectUri,
                                    state);

            var webBrowser = new WebBrowser()
                {
                    AllowWebBrowserDrop = false,
                    Dock = DockStyle.Fill,
                    Name = "webBrowser",
                    ScrollBarsEnabled = false,
                    TabIndex = 0
                };

            webBrowser.DocumentText = @"<head></head><body></body>";  // fake a document so we can set cookies
            webBrowser.Document.Cookie = this.Cookie; // will save the user from having to login if valid session exists
            webBrowser.Url = new Uri(url); // need to do this last so it isnt overwritten
            webBrowser.DocumentCompleted += WebBrowser_DocumentCompleted;

            _wasAutoClosed = false;
            _webBrowserForm = new Form();
            _webBrowserForm.WindowState = FormWindowState.Normal;
            _webBrowserForm.Controls.Add(webBrowser);
            _webBrowserForm.Size = new Size(800, 600);
            _webBrowserForm.Name = "Authorise App";
            _webBrowserForm.FormClosed += _webBrowserForm_FormClosed;

            Application.Run(_webBrowserForm);  
        }

        void _webBrowserForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (!_wasAutoClosed)
            {
                OnAuthoriseComplete(new AuthoriseCompleteArgs(AuthorisationResult.Denied, null));
            }
        }


	    private void WebBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
	    {
            if (e.Url.AbsolutePath == "/ws/oauth2/auth/code.php" || e.Url.AbsolutePath == "/ws/oauth2/auth/error.php")
            {
                AuthorisationResult result = AuthorisationResult.Error;
                string reason = null;
                string state = null;

                var queryTokens = HttpUtility.ParseQueryString(e.Url.Query);

                if (queryTokens["code"] == null)
                {
                    // app was not authorised for some reason
                    if (queryTokens["error"] == "access_denied")
                    {
                        result = AuthorisationResult.Denied;
                    }
                    else
                    {
                        // random error so return description value
                        reason = queryTokens["description"];
                    }                    
                }
                else
                {
                    try
                    {
                        // get name of user who logged in from state
                        state = queryTokens["state"];
                        var re = new Regex(@"(?<=name;)\w*(\+\w*)?", RegexOptions.IgnoreCase);
                        var m = re.Match(state);
                        this.Character = HttpUtility.UrlDecode(m.Value);
                        state = state.Replace("name;" + m.Value, "").Trim();
                        
                        // exchange code for a token
                        this.Token = GetToken(queryTokens["code"]);
                        this.Cookie = (sender as WebBrowser).Document.Cookie;


                        result = AuthorisationResult.Authorised;
                    }
                    catch (Exception ex)
                    {
                        reason = ex.Message;
                    }                    
                }

                // clean up web browser before continuing
                _wasAutoClosed = true;
                _webBrowserForm.Close();
                _webBrowserForm.Dispose();

                // notify anyone who is listening of results
                OnAuthoriseComplete(new AuthoriseCompleteArgs(result, reason, state));
            }
	    }

        private OAuthToken GetToken(string code)
        {
            try
            {
                var values = new Dictionary<string, string> ()
                                {
                                    { "code", code }
                                    ,{ "client_id", this.ClientId }
                                    ,{ "client_secret", this.ClientSecret }
                                    ,{ "redirect_uri", this.RedirectUri }
                                    ,{ "grant_type", GrantTypes.AuthorizationCode }
                                    ,{ "access_type", AccessTypes.Offline }
                                };
                var token = MakeRequest<OAuthToken>(OAuthEnpoints.Token, RequestMethods.Post, values);
                return token;
            }
            catch
            {
                throw;
            }
        }

        private void SavePersistentData()
        {
            if (this.Shared)
            {
                return;
            }

            var data = new PersistentData()
                        {
                            Character = this.Character
                            ,RefreshToken = this.Token.RefreshToken
                            ,Cookie = this.Cookie
                        };
            data.Save();
        }

        private TValue MakeRequest<TValue>(string uri, string method, Dictionary<string, string> values) where TValue : class
        {
            return MakeRequest<TValue>(new Uri(uri), method, values);
        }

        private TValue MakeRequest<TValue>(Uri uri, string method, Dictionary<string, string> values) where TValue : class
        {
            try
            {
                var body = string.Join("&", values.Select(kp => kp.Key + "=" + HttpUtility.UrlEncode(kp.Value)));
                
                if (method == RequestMethods.Get)
                {
                    // values should be query parameters so update uri
                    var uriBuilder = new UriBuilder(uri) { Query = body };
                    uri = uriBuilder.Uri;
                }

                var request = WebRequest.Create(uri) as HttpWebRequest;
                
                request.ProtocolVersion = System.Net.HttpVersion.Version10;
                request.Accept = ContentTypes.JSON;

                if (method != WebRequestMethods.Http.Get)
                {
                    // need to setup body of request
                    var bytes = Encoding.UTF8.GetBytes(body);
                    request.ContentType = ContentTypes.FormData;
                    request.Method = method;
                    request.ContentLength = bytes.Length;

                    var stream = request.GetRequestStream();
                    stream.Write(bytes, 0, bytes.Length);

                    // Close the Stream object.
                    stream.Close();
                }
                else
                {                    
                    request.ContentType = ContentTypes.UTF8;
                    request.Method = method;
                }                

                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception(String.Format("Server error (HTTP {0}: {1}).", response.StatusCode, response.StatusDescription));
                    }

                    var jsonSerializer = new DataContractJsonSerializer(typeof(TValue));
                    object objResponse = jsonSerializer.ReadObject(response.GetResponseStream());
                    return objResponse as TValue;
                }
            }
            catch
            {
                throw;
            }
        }
    }
}
