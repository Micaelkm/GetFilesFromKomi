using FilesToKomi.Request;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace FilesToKomi.UploadToKomi
{
    /// <summary>
    /// Class Utility that contains useful methods
    /// </summary>
    public class Utility
    {
        private static string _downloadFile = ConfigurationManager.AppSettings["DOWNLOADEDDATAFILE"];

        public enum ObChecksumAlgo
        {
            md5,
            sha1,
            sha256,
        };
        /// <summary>
        /// Retrieve result from the HttpWebResponse
        /// </summary>
        /// <param name="response">HttpWebResponse</param>
        /// <returns>string</returns>
        public static string GetResult(HttpWebResponse response)
        {
            if (response == null) throw new WebException("Le delai d'attente de l'opération a expiré !!!");
            var streamReader = new StreamReader(response.GetResponseStream());
            string res = streamReader.ReadToEnd();
            streamReader.Close();
            response.Close();
            return res;

        }

        //todo : modifier le code
        public static bool ValidateServerCertificate(
               object sender,
               X509Certificate certificate,
               X509Chain chain,
               SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }


        /// <summary>
        /// Method parse error returned from V2 WS
        /// </summary>
        /// <param name="ex">WebException</param>
        /// <exception cref="ObException">Exception raised when there is an error due to the WS request</exception>
        /// <exception cref="WebException">Exception raised when there is an error due to the connection</exception>
        /// <exception cref="ArgumentException">Exception raised when there is a json format error</exception>
        public static void ParseError(WebException ex)
        {

            var response = ex.Response as HttpWebResponse;
            if (ex.Status != WebExceptionStatus.ProtocolError || response == null)
            {
                throw ex;
            }

            int statusCode = (int)response.StatusCode;

            try
            {
                JavaScriptSerializer jss = new JavaScriptSerializer();
                Error error = (Error)jss.Deserialize(GetResult((HttpWebResponse)ex.Response), typeof(Error));
                if (error != null && error.error != null)
                {
                    throw new Exception(error.error.message + "\n Code: " + error.error.code + "\n StatusCode: " + statusCode);
                }
                throw new Exception(error.error.message + "" + "\n StatusCode: " + statusCode);
            }
            catch (ArgumentException)
            {
                // throw the original HTTP Exception to presever potiential error in HTTP StatusCode 
                throw new Exception(ex.Message + "" + "\n StatusCode: " + statusCode);
            }
        }

        public static void AffectGPSInfosParamsToGetRequest(ObWsRequest obWsRequest, ref string url)
        {
            if (obWsRequest.GPSInfos != null)
            {
                bool changed = false;
                bool isConcatinated = false;
                if (url.EndsWith("/"))
                {
                    url = url.Remove(url.LastIndexOf('/'));
                    changed = true;
                }

                if (!string.IsNullOrEmpty(obWsRequest.GPSInfos.Lat) && !string.IsNullOrEmpty(obWsRequest.GPSInfos.Lng))
                {
                    url += "?lat=" + obWsRequest.GPSInfos.Lat + "^&lng=" + obWsRequest.GPSInfos.Lng;
                    isConcatinated = true;
                }
                else
                {
                    if (!string.IsNullOrEmpty(obWsRequest.GPSInfos.Lng))
                    {
                        url += "?lng=" + obWsRequest.GPSInfos.Lng;
                        isConcatinated = true;
                    }
                    if (!string.IsNullOrEmpty(obWsRequest.GPSInfos.Lat))
                    {
                        url += "?lat=" + obWsRequest.GPSInfos.Lat;
                        isConcatinated = true;
                    }
                }
                if (changed && !isConcatinated)
                    url += "/";
            }

        }

        public static void AffectGPSInfosParamsToPostRequest(ObWsRequest obWsRequest, ref NameValueCollection nvc)
        {
            if (obWsRequest.GPSInfos != null)
            {
                if (nvc == null) nvc = new NameValueCollection();
                nvc.Add("lat", obWsRequest.GPSInfos.Lat);
                nvc.Add("lng", obWsRequest.GPSInfos.Lng);
            }
        }

        public static byte[] getFileBytes(string Fichier)
        {
            // lit le flux en mode binaire
            FileStream stream = new FileStream(Fichier, FileMode.Open);
            BinaryReader binReader = new BinaryReader(stream);
            byte[] result = binReader.ReadBytes((System.Int32)stream.Length); // lit tous
            stream.Close();

            return result;
        }

        private static WebProxy _proxy;
        private static ObWsRequest _obWsRequest;

        public static WebProxy CreatProxy(string strServerProxy = null, string strLoginProxy = null, string strPwdProxy = null, string strPortProxy = null)
        {
            if (string.IsNullOrEmpty(strServerProxy) && string.IsNullOrEmpty(strLoginProxy)
                && string.IsNullOrEmpty(strPwdProxy) && string.IsNullOrEmpty(strPortProxy))
            {
                _proxy = null;
            }
            else
            {
                string username = strLoginProxy;
                string password = strPwdProxy;
                if (!string.IsNullOrEmpty(strPortProxy)) { _proxy = new WebProxy(strServerProxy, int.Parse(strPortProxy)); }
                else { _proxy = new WebProxy(strServerProxy); }
                // Create a NetworkCredential object and is assign to the Credentials property of the Proxy object.
                if (!string.IsNullOrEmpty(username))
                    _proxy.Credentials = new NetworkCredential(username, password);

                //bool useProxy = !string.Equals(System.Net.WebRequest.DefaultWebProxy.GetProxy(new Uri(proxyAddress)), proxyAddress);
            }

            return _proxy;
        }

        public static ObWsRequest LoadParameterSetting(string strServer = null, string strLogin = null, string strPwd = null, string strPort = null, bool blSsl = false,
            string strServerProxy = null, string strLoginProxy = null, string strPwdProxy = null, string strPortProxy = null,
            string strToken = null, string strLat = null, string strLng = null)
        {
            if (!string.IsNullOrWhiteSpace(strToken))
            {
                _obWsRequest = new ObWsRequest(strToken, strServer,
                                           strPort, false, null, 500000, new GPSInfos(strLat, strLng));
            }
            else
            {
                _obWsRequest = new ObWsRequest(strLogin, strPwd, strServer,
                                           strPort, blSsl, _proxy, 50000, new GPSInfos(strLat, strLng));
            }

            return _obWsRequest;
        }

        /// <summary>
        /// Log Downloaded Documents to JSON Files
        /// </summary>
        /// <param name="documentName"></param>
        /// <param name="documentMedataName"></param>
        public static void LogDownloadedDocuments(string documentName, string documentMedataName, string folderID, bool updateMetadata)
        {
            try
            {
                string fileData = File.ReadAllText(_downloadFile);
                JObject jsonObject = new JObject();
                int version = 0;
                if (string.IsNullOrEmpty(documentMedataName)) { documentMedataName = string.Empty; }

                //Verify if log file has data (Yes : Update Data) (No : Create an Entry)            
                if (!string.IsNullOrEmpty(fileData))
                {
                    jsonObject = JObject.Parse(fileData);

                    //Verify if the document entry exists in log file(Yes : Update Data) (No : Create an Entry) 
                    if (jsonObject[folderID] != null)
                    {
                        if (jsonObject[folderID][documentName] != null)
                        {
                            string documentMetadata = jsonObject[folderID][documentName]["documentMetadata"] != null ? jsonObject[folderID][documentName]["documentMetadata"].ToString() : string.Empty;

                            //Checks whether you need to create an entry for document metadata (Yes : Create) (No : Verify if it's to update (Yes : Update) (No : Do Nothing))
                            if (string.IsNullOrEmpty(documentMetadata))
                            {
                                JObject childItem = new JObject(new JProperty("documentMetadata", documentMedataName), new JProperty("documentMetadataVersion", 1));
                                jsonObject[folderID][documentName] = childItem;
                            }
                            else if (updateMetadata && !string.IsNullOrEmpty(documentMetadata))
                            {
                                version = jsonObject[folderID][documentName]["documentMetadataVersion"] != null ? Convert.ToInt32(jsonObject[folderID][documentName]["documentMetadataVersion"].ToString()) : 0;
                                int newVersion = (version + 1);
                                jsonObject[folderID][documentName]["documentMetadataVersion"] = newVersion;
                            }
                        }
                        else
                        {
                            JObject childItem = new JObject(new JProperty("documentMetadata", documentMedataName), new JProperty("documentMetadataVersion", 1));
                            jsonObject[folderID][documentName] = childItem;
                        }
                    }
                    else
                    {
                        JObject childItem = new JObject(new JProperty("documentMetadata", documentMedataName), new JProperty("documentMetadataVersion", 1));
                        jsonObject[folderID] = new JObject(new JProperty(documentName, childItem));
                    }
                }
                else
                {

                    JObject childItem = new JObject(new JProperty("documentMetadata", documentMedataName), new JProperty("documentMetadataVersion", 1));
                    jsonObject[folderID] = new JObject(new JProperty(documentName, childItem));
                }

                //Update Log File
                using (StreamWriter file = File.CreateText(_downloadFile))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(file, jsonObject);
                }

                GC.Collect();
            }
            catch (Exception e)
            {
                LogToHistory(e.Message.ToString(), "Exception");
            }
        }

        /// <summary>
        /// Verify if document metadata file already Exists
        /// </summary>
        /// <param name="logFile"></param>
        /// <param name="documentName"></param>
        /// <param name="documentMedataName"></param>
        /// <returns></returns>
        public static bool DocumentMetadataExists(string documentName, string folderID)
        {
            try
            {
                string fileData = File.ReadAllText(_downloadFile);
                if (!string.IsNullOrEmpty(fileData))
                {
                    JObject jsonObject = JObject.Parse(fileData);
                    if (jsonObject[folderID] != null)
                    {
                        string documentMetadata = jsonObject[folderID]["documentMetadata"] != null ? jsonObject[folderID]["documentMetadata"].ToString() : string.Empty;
                        string folderDocName = jsonObject[folderID][documentName] != null ? jsonObject[folderID][documentName].ToString() : string.Empty;

                        if (!string.IsNullOrEmpty(documentMetadata) && (folderDocName == documentName && !string.IsNullOrEmpty(folderDocName))) { return true; }

                    }
                }

            }
            catch (Exception e)
            {
                LogToHistory(e.Message.ToString(), "Exception");
            }
            return false;
        }

        /// <summary>
        /// Verify if document alreadyExiss
        /// </summary>
        /// <param name="logFile"></param>
        /// <param name="documentName"></param>
        /// <param name="documentMedataName"></param>
        /// <returns></returns>
        public static bool DocumentExistsInFolder(string documentName, string folderID)
        {
            try
            {
                string fileData = File.ReadAllText(_downloadFile);
                if (!string.IsNullOrEmpty(fileData))
                {
                    JObject jsonObject = JObject.Parse(fileData);
                    if (jsonObject[folderID] != null)
                    {
                        string folderDocName = jsonObject[folderID][documentName] != null ? jsonObject[folderID][documentName].ToString() : string.Empty;
                        if (folderDocName != documentName && !string.IsNullOrEmpty(folderDocName))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogToHistory(e.Message.ToString(), "Exception");
            }

            return false;
        }

        /// <summary>
        /// Remove special characters like accents
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveDiacritics(string str)
        {
            string cleanStr = string.Empty;
            if (!string.IsNullOrEmpty(str))
            {
                var chars =
                    from c in str.Normalize(NormalizationForm.FormD).ToCharArray()
                    let uc = CharUnicodeInfo.GetUnicodeCategory(c)
                    where uc != UnicodeCategory.NonSpacingMark
                    select c;

                cleanStr = new string(chars.ToArray()).Normalize(NormalizationForm.FormC);
            }

            return cleanStr;
        }

        /// <summary>
        /// Only Fist char is upper case
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string FirstCharToUpper(string input)
        {
            if (String.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToUpper() + input.Substring(1);
        }

        /// <summary>
        /// Format String  based on special chars, case sensitive and spaces
        /// </summary>
        /// <param name="element"></param>
        /// <param name="removeSpaces"></param>
        /// <param name="caseSense"></param>
        /// <param name="replaceChars"></param>
        /// <returns></returns>
        public static string FormatString(string element, bool removeSpaces, string caseSense, bool replaceChars)
        {
            string formatedElement = element;
            if (removeSpaces) { formatedElement = Regex.Replace(formatedElement, @"\s+", ""); }
            if (replaceChars) { formatedElement = Utility.RemoveDiacritics(formatedElement); }

            switch (caseSense)
            {
                case "upper":
                    formatedElement = formatedElement.ToUpper();
                    break;
                case "lower":
                    formatedElement = formatedElement.ToLower();
                    break;
                case "firstupper":
                    formatedElement = Utility.FirstCharToUpper(formatedElement);
                    break;
                case "capitalize":
                    formatedElement = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(formatedElement);
                    break;
                default:
                    formatedElement = Utility.FirstCharToUpper(formatedElement);
                    break;
            }

            return formatedElement;
        }


        /// <summary>
        /// Log Info to history file
        /// </summary>
        /// <param name="logInfo"></param>
        /// <param name="logType"></param>
        public static void LogToHistory(string logInfo, string logType)
        {

            using (StreamWriter file = new StreamWriter(Program._logFile, true))
            {
                if (!string.IsNullOrEmpty(logInfo) && !string.IsNullOrEmpty(logType))
                {
                    string newLine = string.Format("{0} | {1} : {2}", DateTime.Now.ToString(), logType, logInfo);
                    file.WriteLine(newLine);
                    file.Close();
                    file.Dispose();
                }

            }

            GC.Collect();
        }
    }

    /// <summary>
    /// Custom error params
    /// </summary>
    public class ErrorInfos
    {
        /// <summary>
        /// Custom error code
        /// </summary>
        public string code;
        /// <summary>
        /// Custom error message
        /// </summary>
        public string message;
    }

    /// <summary>
    /// Custom error
    /// //@todo remove this class
    /// </summary>
    public class Error
    {
        public ErrorInfos error;
    }

}
