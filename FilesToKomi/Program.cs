using FilesToKomi.UploadToKomi;
using System;
using System.Configuration;
using System.IO;



namespace FilesToKomi
{
    class Program
    {
        public static string _logFile;

        static void Main(string[] args)
        {
            string docFolder = ConfigurationManager.AppSettings["DOCFOLDER"];
            string logfolder = ConfigurationManager.AppSettings["DOCUMENTLOGSFOLDER"];
            _logFile = string.Format("{0}\\log_{1}.txt", logfolder, DateTime.Now.ToString("yyyyMMdd_hhmm"));
            string downloadFile = ConfigurationManager.AppSettings["DOWNLOADEDDATAFILE"];


            try
            {
                if (!Directory.Exists(docFolder)) { Directory.CreateDirectory(docFolder); }
                if (!Directory.Exists(logfolder)) { Directory.CreateDirectory(logfolder); }
                if (!File.Exists(_logFile)) { var newFile = File.Create(_logFile); newFile.Close(); }
                if (!File.Exists(downloadFile)) { var newFile = File.Create(downloadFile); newFile.Close(); }

                Console.WriteLine("Start Downloading documents from KomiDoc...");
                FileManager.DownloadDocuments();
                Console.WriteLine("Downloading documents is complete.");

            }
            catch (Exception e)
            {
                Utility.LogToHistory(e.Message.ToString(), "Exception");
                Console.WriteLine("An error ocurred , check log file in this location : " + _logFile);
                Console.WriteLine("Press any key to exit...");
                Console.ReadLine();
            }
        }
    }
}
