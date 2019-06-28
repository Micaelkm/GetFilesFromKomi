using FilesToKomi.UploadToKomi;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;

namespace FilesToKomi.Request
{
    /// <summary>
    /// Class ObWsRequest
    /// </summary>
    public class ObWsRequest
    {
        public ObWsRequest(String configFilePath = null)
        {

            WebProxy proxy = null;

            //App Settings
            string username = ConfigurationManager.AppSettings["USERNAME"];

            string password = ConfigurationManager.AppSettings["PASSWORD"];

            string server = ConfigurationManager.AppSettings["ADDRESS"];
            string port = ConfigurationManager.AppSettings["PORT"];
            Boolean ssl = Boolean.Parse(ConfigurationManager.AppSettings["SSL"]);


            this.connection(username, password, server, port, ssl, proxy, Timeout.Infinite, null);
        }

        /// <summary>
        /// Constructor of the object that contains the common parameters of the request
        /// </summary>
        /// <param name="userName">login</param>
        /// <param name="password">password</param>
        /// <param name="server">server name or adress</param>
        /// <param name="port">port</param>
        /// <param name="ssl">true : secured connection | false:non secured connection</param>
        /// <param name="proxy">WebProxy</param>
        /// <seealso cref="System.Net">WebProxy</seealso>
        /// <param name="timeOut">time max of the request</param>
        /// <param name="gpsInfos">GPSInfos</param>
        /// <seealso cref="OpenBeeApi.Common.Request.GPSInfos">GPSInfos</seealso>
        public ObWsRequest(string userName, string password, string server, string port, bool ssl, WebProxy proxy, int timeOut, GPSInfos gpsInfos)
        {
            this.connection(userName, password, server, port, ssl, proxy, timeOut, gpsInfos);
        }

        /// <summary>
        /// Constructor of the object that contains the common parameters of the request
        /// </summary>
        /// <param name="userName">login</param>
        /// <param name="password">password</param>
        /// <param name="server">server name or adress</param>
        /// <param name="port">port</param>
        /// <param name="ssl">true : secured connection | false:non secured connection</param>
        /// <param name="proxy">WebProxy</param>
        /// <seealso cref="System.Net">WebProxy</seealso>
        /// <param name="timeOut">time max of the request</param>
        /// <param name="gpsInfos">GPSInfos</param>
        /// <seealso cref="OpenBeeApi.Common.Request.GPSInfos">GPSInfos</seealso>
        public ObWsRequest(string userName, string password, string server, string port, bool ssl, WebProxy proxy, int timeOut, GPSInfos gpsInfos, bool httpWebRequestKeepAlive)
        {
            this.connection(userName, password, server, port, ssl, proxy, timeOut, gpsInfos);
            HttpWebRequestKeepAlive = httpWebRequestKeepAlive;
        }

        public ObWsRequest(string userName, string password, string server, string port, bool ssl, WebProxy proxy, int timeOut, GPSInfos gpsInfos, bool httpWebRequestKeepAlive, bool sendChunked, bool allowWriteStreamBuffering)
        {
            this.connection(userName, password, server, port, ssl, proxy, timeOut, gpsInfos);
            HttpWebRequestKeepAlive = httpWebRequestKeepAlive;
            SendChunked = sendChunked;
            AllowWriteStreamBuffering = allowWriteStreamBuffering;
        }


        public ObWsRequest(string token, string server, string port, bool ssl, WebProxy proxy, int timeOut, GPSInfos gpsInfos)
        {
            Token = token;
            Server = server;
            Port = port;
            Ssl = ssl;
            Proxy = proxy;
            Url = !Ssl
                      ? (string.IsNullOrEmpty(port) ? "http://" + Server : "http://" + Server + ":" + Port)
                      : (string.IsNullOrEmpty(port) ? "https://" + Server : "https://" + Server + ":" + Port);
            ServicePointManager.ServerCertificateValidationCallback += Utility.ValidateServerCertificate;
            TimeOut = timeOut;
            GPSInfos = gpsInfos;
        }

        public void connection(string userName, string password, string server, string port, bool ssl, WebProxy proxy, int timeOut, GPSInfos gpsInfos)
        {
            UserName = userName;
            Password = password;
            Server = server;
            Port = port;
            Ssl = ssl;
            Proxy = proxy;
            AuthInfo = UserName + ":" + Password;
            AuthInfo = Convert.ToBase64String(Encoding.Default.GetBytes(AuthInfo));
            //Url = "http://" + Server + ":" + Port;
            Url = !Ssl
                      ? (string.IsNullOrEmpty(port) ? "http://" + Server : "http://" + Server + ":" + Port)
                      : (string.IsNullOrEmpty(port) ? "https://" + Server : "https://" + Server + ":" + Port);
            ServicePointManager.ServerCertificateValidationCallback += Utility.ValidateServerCertificate;
           
            TimeOut = timeOut;
            GPSInfos = gpsInfos;
        }

        /// <summary>
        /// login of the user
        /// </summary>
        protected string UserName;

        /// <summary>
        /// password of the user
        /// </summary>
        protected string Password;

        /// <summary>
        /// Token of the user for OAuth connection
        /// </summary>
        public string Token;

        /// <summary>
        /// server name or adress
        /// </summary>
        public string Server;

        /// <summary>
        /// port
        /// </summary>
        public string Port;

        /// <summary>
        /// true : secured connection | false:non secured connection
        /// </summary>
        public bool Ssl;

        /// <summary>
        /// Proxy
        /// </summary>
        public WebProxy Proxy;

        /// <summary>
        /// Information of authentication
        /// </summary>
        public string AuthInfo;

        /// <summary>
        /// Uri of the request
        /// </summary>
        public string Url;

        /// <summary>
        /// time max of the request
        /// </summary>
        public int TimeOut;

        /// <summary>
        /// object that defines the coords of the GPS
        /// </summary>
        public GPSInfos GPSInfos;

        /// <summary>
        /// object that defines the property keep-alive of the request
        /// </summary>
        public bool HttpWebRequestKeepAlive;

        public bool SendChunked = false;
        public bool AllowWriteStreamBuffering = true;
    }

    /// <summary>
    /// Class WsRequest
    /// </summary>
    public class WsRequest
    {
        protected HttpWebRequest WebRequest { get; set; }
        /// <summary>
        /// Return a reponse of an Internet ressource
        /// </summary>
        /// <returns>HttpWebResponse</returns>
        protected internal HttpWebResponse GetResponse()
        {
            HttpWebResponse response;// = null
            try
            {
                DateTime startTimeResponseReceived, stopTimeResponseReceived;
                TimeSpan ts = new TimeSpan();
                startTimeResponseReceived = DateTime.Now;
                response = WebRequest.GetResponse() as System.Net.HttpWebResponse;
                stopTimeResponseReceived = DateTime.Now;
                ts = stopTimeResponseReceived - startTimeResponseReceived;

                return response;
            }
            catch (WebException ex)
            {
                ServicePoint servicePoint = ServicePointManager.FindServicePoint(ex.Response.ResponseUri);
                if (servicePoint.ProtocolVersion < HttpVersion.Version11)
                {
                    int maxIdleTime = servicePoint.MaxIdleTime;
                    servicePoint.MaxIdleTime = 0;
                    Thread.Sleep(1);
                    servicePoint.MaxIdleTime = maxIdleTime;
                }
                throw new Exception(ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                response = null;
                GC.Collect();
            }
        }

        /// <summary>
        /// Return a reponse of an Internet ressource
        /// </summary>
        /// <returns>HttpWebResponse</returns>
        /// <exception cref="System.Net.WebException">Exception raised when there is an error due to the connection</exception>
        /// <exception cref="System.Exception">Exception raised when there is a system error</exception>
        protected internal HttpWebResponse GetResponseV2()
        {
            try
            {
                return GetResponse();
            }
            catch (WebException ex)
            {
                Utility.ParseError(ex);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            return null;
        }
    }

    /// <summary>
    /// Class GPSInfos
    /// </summary>
    public class GPSInfos
    {
        /// <summary>
        /// Constructor of the object that defines the coords of the GPS
        /// </summary>
        /// <param name="lat">string : longitude</param>
        /// <param name="lng">string : latitude</param>
        public GPSInfos(string lat, string lng)
        {
            Lat = lat;
            Lng = lng;
        }

        /// <summary>
        /// longitude
        /// </summary>
        public string Lng { get; set; }

        /// <summary>
        /// latitude
        /// </summary>
        public string Lat { get; set; }
    }
}
