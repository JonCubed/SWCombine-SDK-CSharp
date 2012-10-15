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
using System.Diagnostics;

namespace SWCombine.SDK
{
    public delegate void AuthoriseCompleteHandler(object sender, AuthoriseCompleteEventArgs e);

    public class SWC
    {
        #region Members

        private const string Out_Of_Band_Uri = "urn:ietf:wg:oauth:2.0:oob";
        private Form _webBrowserForm;
        private AuthListener _server;
        private static bool _wasAutoClosed = false;

        #endregion

        #region Properties

        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUri { get; private set; }
        public string Character { get; private set; }
        public OAuthToken Token { get; private set; }
        private string Cookie { get; set; }
        public bool Shared { get; private set; }
        public int? Port { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Event is raised once authorisation process is complete
        /// regardless of result.
        /// </summary>
        public event AuthoriseCompleteHandler AuthoriseComplete;

        /// <summary>
        /// Raises the <see cref="AuthoriseComplete"/> event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnAuthoriseComplete(AuthoriseCompleteEventArgs e)
        {
            if (AuthoriseComplete != null)
            {
                AuthoriseComplete(this, e);
            }            
        }

        #endregion

        #region Public

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
                ,Port = port
            };

            if (!shared)
            {
                // load persistent data
                var data = PersistentData.Load();

                if (data != null && !string.IsNullOrEmpty(data.Cookie))
                {
                    swc.Cookie = data.Cookie;
                }

                if (data != null && !string.IsNullOrEmpty(data.RefreshToken))
                {
                    swc.Token = new OAuthToken() { RefreshToken = data.RefreshToken };
                }
            }

            return swc;
        }

        /// <summary>
        /// Attempts authorise process by opening a web browser for the user.
        /// </summary>
        /// <param name="scopes">List of scopes required by the app for user to authorise.</param>
        /// <param name="state">Any state information to be pass back to the app on completion of authorisation.</param>
        public void AttemptAuthorise(List<string> scopes, string state)
        {
            if (_webBrowserForm != null)
            {
                Debug.WriteLine("Login in progress, must explicitly abort current login.");
                throw new ApplicationException("Login in progress, must explicitly abort current login.");
            }

            var url = String.Format("{0}?response_type=code&client_id={1}&scope={2}&redirect_uri={3}&state={4}",
                                    OAuthEnpoints.Auth,
                                    this.ClientId,
                                    string.Join(" ", scopes),
                                    this.RedirectUri,
                                    state);

            // a web browser that we can monitor and control
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

            if (this.Port.HasValue)
            {
                // if we have a port use it as the prefer method
                try
                {
                    _server = new AuthListener();
                    _server.RequestComplete += Server_RequestComplete;
                    _server.ListenTo(this.Port.Value);
                    webBrowser.DocumentCompleted += WebBrowser_SaveCookies;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("AuthListener failed to start giving the reason: {0}", ex.Message);

                    // revert to monitoring web browser method
                    webBrowser.DocumentCompleted += WebBrowser_DocumentCompleted;
                    url = url.Replace(this.RedirectUri, Out_Of_Band_Uri);
                    webBrowser.Url = new Uri(url);
                }
            }
            else
            {
                // this is a less secure method but unlikely to fail
                webBrowser.DocumentCompleted += WebBrowser_DocumentCompleted;
            }

            _wasAutoClosed = false;
            _webBrowserForm = new Form();
            _webBrowserForm.WindowState = FormWindowState.Normal;
            _webBrowserForm.Controls.Add(webBrowser);
            _webBrowserForm.Size = new Size(800, 600);
            _webBrowserForm.Name = "Authorise App";
            _webBrowserForm.Text = "Authorise an app";
            _webBrowserForm.FormClosed += _webBrowserForm_FormClosed;

            Application.Run(_webBrowserForm);
        }

        #endregion

        #region Private

        /// <summary>
        /// Parses the query tokens returned during authorisation process
        /// and notifies app of result.
        /// </summary>
        /// <param name="queryTokens">Query tokens to parse for authorisation.</param>
        private void ParseUrl(NameValueCollection queryTokens)
        {
            AuthorisationResult result = AuthorisationResult.Error;
            string reason = null;
            string state = null;

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

                    result = AuthorisationResult.Authorised;
                }
                catch (Exception ex)
                {
                    reason = ex.Message;
                }
            }

            // clean up web browser before continuing
            _wasAutoClosed = true;
            CloseForm(_webBrowserForm);

            // notify anyone who is listening of results
            OnAuthoriseComplete(new AuthoriseCompleteEventArgs(result, reason, state));
        }

        /// <summary>
        /// Closes the given form making sure that we are on the correct thread.
        /// </summary>
        /// <param name="form">Form that we would like to close.</param>
        private void CloseForm(Form form)
        {
            if (form.InvokeRequired)
            {
                form.Invoke(new Action<Form>(CloseForm), form);
            }

            form.Close();
            form.Dispose();
        }

        /// <summary>
        /// Exchanges access_code for tokens.
        /// </summary>
        /// <param name="code">Access code to exchange for tokens.</param>
        /// <returns>If successful, returns tokens that can be used by the app.</returns>
        private OAuthToken GetToken(string code)
        {
            try
            {
                var values = new Dictionary<string, string>()
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

        /// <summary>
        /// Saves any data that we need to persist between app instances
        /// </summary>
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

        #region Make A Request

        /// <summary>
        /// Makes a request to the specified uri.
        /// </summary>
        /// <typeparam name="TValue">Type to convert response body to.</typeparam>
        /// <param name="uri">Uri to make the request to.</param>
        /// <param name="method">Http Method to use for the request.</param>
        /// <param name="values">Any parameters to include in the request, where the Key is the parameter name and Value is the parameter value.</param>
        /// <returns>If successful returns a instance of TValue</returns>
        private TValue MakeRequest<TValue>(string uri, string method, Dictionary<string, string> values) where TValue : class
        {
            return MakeRequest<TValue>(new Uri(uri), method, values);
        }

        /// <summary>
        /// Makes a request to the specified uri.
        /// </summary>
        /// <typeparam name="TValue">Type to convert response body to.</typeparam>
        /// <param name="uri">Uri to make the request to.</param>
        /// <param name="method">Http Method to use for the request.</param>
        /// <param name="values">Any parameters to include in the request.</param>
        /// <returns>If successful returns a instance of TValue</returns>
        /// <exception cref="System.Net.WebException"></exception>
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
                        var error = SWCHelper.JsonTo<RequestError>(response.GetResponseStream());

                        throw new RequestException(error, response.StatusCode, response.StatusDescription);
                    }

                    var content = SWCHelper.JsonTo<TValue>(response.GetResponseStream());
                    return content;
                }
            }
            catch (WebException ex)
            {
                var error = SWCHelper.JsonTo<RequestError>(ex.Response.GetResponseStream());

                throw new RequestException(error, ex);
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion
        
        #endregion

        #region Event Handlers

        /// <summary>
        /// Process Authorisation result when using localhost method.
        /// </summary>
        private void Server_RequestComplete(object sender, RequestCompleteEventArgs e)
        {
            var server = sender as AuthListener;
            server.Stop();

            ParseUrl(e.QueryTokens);
        }

        /// <summary>
        /// Cleans up when web browser form is closed.
        /// </summary>
        void _webBrowserForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (!_wasAutoClosed)
            {
                if (this.Port.HasValue && this._server != null)
                {
                    // clean up Authorisation Listener
                    _server.Stop();
                    _server = null;
                }

                OnAuthoriseComplete(new AuthoriseCompleteEventArgs(AuthorisationResult.Denied, null));
            }
        }

        /// <summary>
        /// Save browser cookies so we can reuse them at a later time.
        /// </summary>
        private void WebBrowser_SaveCookies(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (e.Url.AbsolutePath == "/ws/oauth2/auth/code.php" || e.Url.AbsolutePath == "/ws/oauth2/auth/error.php")
            {
                // save our cookies so we can auto login later
                var webBrowser = sender as WebBrowser;
                this.Cookie = webBrowser.Document.Cookie;
            }
        }

        /// <summary>
        /// Process Authorisation result when using poll web browser method.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (e.Url.AbsolutePath == "/ws/oauth2/auth/code.php" || e.Url.AbsolutePath == "/ws/oauth2/auth/error.php")
            {
                WebBrowser_SaveCookies(sender, e);

                ParseUrl(HttpUtility.ParseQueryString(e.Url.Query));
            }
        }

        #endregion
       
    }
}
