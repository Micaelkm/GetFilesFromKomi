using System;
using System.IO;
using System.Net;
using FilesToKomi.Request;
using System.Configuration;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Xml;

namespace FilesToKomi.UploadToKomi
{
    public class FileManager
    {
        private static WebProxy _proxy;
        private static ObWsRequest _obWsRequest;
        private static string _rootDonwloadFolderPath;
        private static string _downloadFolder;
        private static string _documentFilePath;
        private static string _documentMetadataPath;
        private static string _documentName;
        private static string _documentKomiDocID;
        private static string _folderID;
        private static bool _updateMetadata;
        private static bool _downloadMetadata;
        private static bool _downloadDocument;
        private static bool _ssl;
        private static int totalDocumentsDownloaded;
        private static int totalFolders;

        public static void DownloadDocuments()
        {
            _rootDonwloadFolderPath = ConfigurationManager.AppSettings["DOCFOLDER"];
            _updateMetadata = Convert.ToBoolean(ConfigurationManager.AppSettings["UPDATEMETADATA"]);
            _downloadMetadata = Convert.ToBoolean(ConfigurationManager.AppSettings["DOWNLOADMETADATA"]);
            _downloadDocument = Convert.ToBoolean(ConfigurationManager.AppSettings["DOWNLOADDOCUMENT"]);

            _proxy = Utility.CreatProxy();
            _ssl = Convert.ToBoolean(ConfigurationManager.AppSettings["SSL"]);
            string foldersConfigurationPath = ConfigurationManager.AppSettings["FOLDERSCONFIGURATIONPATH"];
            string strLog = string.Empty;
            _obWsRequest = Utility.LoadParameterSetting(ConfigurationManager.AppSettings["ADDRESS"], ConfigurationManager.AppSettings["USERNAME"], ConfigurationManager.AppSettings["PASSWORD"], ConfigurationManager.AppSettings["PORT"], _ssl);


            using (StreamReader fStream = new StreamReader(foldersConfigurationPath))
            {
                JObject foldersConfigFile = JObject.Parse(fStream.ReadToEnd());

                string folderID = string.Empty;
                bool download = false;

                foreach (var folder in foldersConfigFile["folders"])
                {
                    folderID = folder["folderID"] != null ? folder["folderID"].ToString() : string.Empty;
                    download = folder["download"] != null ? Convert.ToBoolean(folder["download"].ToString()) : false;

                    if (download)
                    {
                        GetFolders(folderID);

                        strLog = string.Format("Have been downloaded {0} documents in {1} folders ", totalDocumentsDownloaded, totalFolders);
                        Utility.LogToHistory(strLog, "Information");

                        GC.Collect();
                    }

                    GC.Collect();
                }
            }
        }

        public static void GetFolders(string folderID)
        {
            string idSubFolder = string.Empty;
            string idDocument = string.Empty;
            string wsStatusDocument = string.Empty;
            string folderUrl = string.Format("{0}/ws/v2/folder/{1}", _obWsRequest.Url, folderID);

            JObject requestedFolderObject = wsRequest(_obWsRequest.AuthInfo, folderUrl);
            JArray folders = JArray.Parse(requestedFolderObject["folders"].ToString());
            JArray documents = JArray.Parse(requestedFolderObject["documents"].ToString());

            string str = string.Format("Folder {0} has  {1} documents", folderID, documents.Count);
            totalDocumentsDownloaded += documents.Count;
            totalFolders += folders.Count;

            Utility.LogToHistory(str, "Information");
            Console.WriteLine(str);

            //Get Documents
            if (documents.Count > 0)
            {
                _folderID = folderID;
                _downloadFolder = string.Format("{0}\\{1}", _rootDonwloadFolderPath, folderID);
                if (!Directory.Exists(_downloadFolder)) { Directory.CreateDirectory(_downloadFolder); }

                decimal count = 0;
                foreach (var document in documents)
                {
                    //Progess Info                   
                    count++;
                    Console.WriteLine("Downloaded {0} documents out of {1} ", count, documents.Count);
                    idDocument = document["document"]["idDocument"] != null ? document["document"]["idDocument"].ToString() : string.Empty;
                    wsStatusDocument = document["document"]["wfStatus"] != null ? document["document"]["wfStatus"].ToString() : string.Empty;

                    if (_updateMetadata)
                    {
                        GetDocument(idDocument);
                    }
                    else if (wsStatusDocument == "3" || wsStatusDocument == "2")
                    {
                        GetDocument(idDocument);
                    }

                    GC.Collect();
                }
            }

            //Get SubFolders
            if (folders.Count > 0)
            {
                foreach (var folder in folders)
                {
                    idSubFolder = folder["folder"]["idFolder"] != null ? folder["folder"]["idFolder"].ToString() : string.Empty;
                    GetFolders(idSubFolder);

                    GC.Collect();
                }

            }

            GC.Collect();
        }

        /// <summary>
        /// GetDocument - Obtain a document from KomiDoc : Download it and generate a XML with metadata associated
        /// </summary>
        /// <param name="idDocument"></param>
        private static void GetDocument(string idDocument)
        {
            string fileType = ConfigurationManager.AppSettings["METADATAFILETYPE"];
            string documentUrl = string.Format("{0}/ws/v2/document/{1}", _obWsRequest.Url, idDocument);
            JObject requestedDocumentObject = wsRequest(_obWsRequest.AuthInfo, documentUrl);
            JToken documentInfo = requestedDocumentObject["document"];
            JArray documentMetadata = JArray.Parse(requestedDocumentObject["metadatas"].ToString());
            _documentName = documentInfo["name"] != null ? documentInfo["name"].ToString() : string.Empty;
            _documentKomiDocID = documentInfo["idDocument"] != null ? documentInfo["idDocument"].ToString() : string.Empty;

            if (_downloadDocument) { DownloadFile(documentInfo); }

            if (documentMetadata.Count > 0 && _downloadMetadata)
            {
                if (!Utility.DocumentMetadataExists(_documentName, _folderID) || (Utility.DocumentMetadataExists(_documentName, _folderID) && _updateMetadata))
                {
                    switch (fileType)
                    {
                        case "XML":
                            MetadataToXML(documentMetadata);
                            break;
                        case "JSON":
                            MetadataToJSON(documentMetadata);
                            break;
                    }
                }
            }

            Utility.LogDownloadedDocuments(_documentName, _documentMetadataPath, _folderID, _updateMetadata);

            GC.Collect();

        }

        /// <summary>
        /// DownloadFile - Download document from KomiDoc
        /// </summary>
        /// <param name="docInfo"></param>
        public static void DownloadFile(JToken docInfo)
        {
            string documentExtension = docInfo["extension"] != null ? docInfo["extension"].ToString() : string.Empty;
            string documentFileURL = docInfo["fileUri"] != null ? docInfo["fileUri"].ToString() : string.Empty;


            if (!string.IsNullOrEmpty(documentFileURL) && !string.IsNullOrEmpty(documentExtension) && !string.IsNullOrEmpty(_documentName))
            {
                _documentFilePath = string.Format("{0}\\{1}.{2}", _downloadFolder, _documentName, documentExtension);

                if (!Utility.DocumentExistsInFolder(_documentName, _folderID))
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers["Authorization"] = "Basic " + _obWsRequest.AuthInfo;
                        wc.DownloadFile(documentFileURL, _documentFilePath);
                    }
                }
            }

            GC.Collect();
        }

        /// <summary>
        /// MetadataToXML - Generates a XML document with document metadata
        /// </summary>
        /// <param name="docMetadata"></param>
        private static void MetadataToXML(JArray docMetadataParams)
        {
            _documentMetadataPath = string.Format("{0}\\{1}.xml", _downloadFolder, _documentName);
            string nodeName = string.Empty;
            string nodeValue = string.Empty;


            XmlDocument doc = new XmlDocument();

            //XML Header
            XmlNode docNode = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.AppendChild(docNode);

            //XML Metadata Node
            XmlNode metadataNode = doc.CreateElement("Metadata");
            doc.AppendChild(metadataNode);

            foreach (var param in docMetadataParams)
            {
                nodeName = param["metadata"]["name"] != null ? Utility.FormatString(param["metadata"]["name"].ToString(), true, "lower", true) : string.Empty;
                nodeValue = param["metadata"]["value"] != null ? param["metadata"]["value"].ToString() : string.Empty;

                XmlNode node = doc.CreateElement(nodeName);
                node.AppendChild(doc.CreateTextNode(nodeValue));
                metadataNode.AppendChild(node);

                GC.Collect();

            }

            //Path Node
            XmlNode pathNode = doc.CreateElement("documentpath");
            pathNode.AppendChild(doc.CreateTextNode(_documentFilePath));
            metadataNode.AppendChild(pathNode);

            //Name Node
            XmlNode nameNode = doc.CreateElement("documentname");
            nameNode.AppendChild(doc.CreateTextNode(_documentName));
            metadataNode.AppendChild(nameNode);

            //KomiDoc ID Node
            XmlNode komidocIDNode = doc.CreateElement("documentid");
            komidocIDNode.AppendChild(doc.CreateTextNode(_documentKomiDocID));
            metadataNode.AppendChild(komidocIDNode);

            doc.Save(_documentMetadataPath);

            GC.Collect();

        }

        /// <summary>
        /// MetadataToJSON - Generates a JSON document with document metadata
        /// </summary>
        /// <param name="documentMetadata"></param>
        private static void MetadataToJSON(JArray docMetadataParams)
        {
            _documentMetadataPath = string.Format("{0}\\{1}.json", _downloadFolder, _documentName);
            string propName = string.Empty;
            string propValue = string.Empty;

            using (StreamWriter file = File.CreateText(_documentMetadataPath))
            {
                JObject childObject = new JObject();
                foreach (var param in docMetadataParams)
                {
                    propName = param["metadata"]["name"] != null ? Utility.FormatString(param["metadata"]["name"].ToString(), true, "lower", true) : string.Empty;
                    propValue = param["metadata"]["value"] != null ? param["metadata"]["value"].ToString() : string.Empty;
                    JProperty newProperty = new JProperty(propName, propValue);
                    childObject.Add(newProperty);

                    GC.Collect();
                }


                JObject jsonObject = new JObject();
                jsonObject["Metadata"] = childObject;
                jsonObject["Document"] = new JObject(
                    new JProperty("path", _documentFilePath),
                    new JProperty("name", _documentName),
                    new JProperty("id", _documentKomiDocID));

                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, jsonObject);
            }

            GC.Collect();
        }

        /// <summary>
        /// wsRequest - Call a KomiDoc GET Service and returns a JSON Object
        /// </summary>
        /// <param name="authentication"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        public static JObject wsRequest(string authentication, string url)
        {
            string strResponse = string.Empty;
            /**
             * Request 
             */
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Accept = "application/json; charset=utf-8";
            request.Headers["Authorization"] = "Basic " + authentication;

            /**
             * Response
             */
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                strResponse = sr.ReadToEnd();

            }

            return JObject.Parse(strResponse);
        }
    }
}
