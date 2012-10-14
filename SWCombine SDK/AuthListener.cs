using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Runtime.Serialization.Json;
using System.Web;

namespace SWCombine.SDK.Web
{
    public delegate void RequestCompleteHandler(object sender, RequestCompleteEventArgs e);
    
    /// <summary>
    /// Used to retrieve results from the Authorisation process.
    /// </summary>
    /// <remarks>This is used in the localhost method which is the preferred method.</remarks>
    class AuthListener
    {
        #region Members

        private HttpListener _server;

        #endregion
        
        #region Events

        /// <summary>
        /// When the http listener recieves a request.
        /// </summary>
        public event RequestCompleteHandler RequestComplete;

        protected virtual void OnRequestComplete(RequestCompleteEventArgs e)
        {
            if (RequestComplete != null)
            {
                RequestComplete(this, e);
            }
        }
        
        #endregion

        #region Public

        /// <summary>
        /// Starts the Authorisation listener to listen to the specified port.
        /// </summary>
        /// <param name="port"></param>
        public void ListenTo(int port)
        {
            if (!HttpListener.IsSupported)
            {
                Debug.Write("AuthListener unsupported: Windows XP SP2 or Server 2003 is required");
                throw new NotSupportedException("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
            }
            
            try
            {                
                this._server = new HttpListener();

                var prefix = String.Format("http://localhost:{0}/", port);
                this._server.Prefixes.Add(prefix);
                Debug.WriteLine("Prefix " + prefix + " added.");

                // Start listening for client requests.
                this._server.Start();
                Debug.WriteLine("Waiting for a connection... ");

                // Start waiting for a request
                _server.BeginGetContext(new AsyncCallback(ListenerCallback), _server);                
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
                this.Stop();
            }
        }

        /// <summary>
        /// Stops the authorisation listener and cleans up.
        /// </summary>
        public void Stop()
        {
            try
            {
                // Stop listening for new clients.
                _server.Stop();
                _server = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to stop AuthListener: {0}", ex.Message);
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles requests to http listener.
        /// </summary>
        private void ListenerCallback(IAsyncResult result)
        {
            try
            {
                var listener = (HttpListener) result.AsyncState;

                // Call EndGetContext to complete the asynchronous operation.
                var context = listener.EndGetContext(result);
                var request = context.Request;

                // make sure we only process get request from swc server
                if (request.UrlReferrer != null && request.UrlReferrer.Host == "dev.swcombine.net" && request.HttpMethod == RequestMethods.Get)
                {
                    OnRequestComplete(new RequestCompleteEventArgs(request.QueryString));
                }
                else
                {
                    _server.BeginGetContext(new AsyncCallback(ListenerCallback), listener); 
                }                
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to process request: {0}", ex);
            }            
        }

        #endregion
        
    }
}