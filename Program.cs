using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DriveQuickstart
{
    class Program
    {
        //Install-Package Google.Apis.Drive.v3

        // If modifying these scopes, delete your previously saved credentials
        // at ~/.credentials/drive-dotnet-quickstart.json
        static string[] Scopes = { DriveService.Scope.DriveReadonly };
        static string ApplicationName = "Google Drive Trash Download";

        static int Main(string[] args)
        {

            if (args.Length == 0)
            {
                System.Console.WriteLine("Please enter a location to save the files from Google Drive Trash.");
                return 0;
            }

            UserCredential credential;
            string location = args[0].ToString(); 

            using (var stream =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens
                // It is created automatically when the authorization flow completes for the first time
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });


            List<Google.Apis.Drive.v3.Data.File> files = new List<Google.Apis.Drive.v3.Data.File>();

            Console.WriteLine("Date and Time: " + DateTime.Now);
            Console.WriteLine("Looking for files...");
            int retryNumber = 0;
            int totalFileNumber = 0;

            Google.Apis.Drive.v3.Data.FileList result = null;

            while (true)
            {
                if (result != null && string.IsNullOrWhiteSpace(result.NextPageToken))
                    break;

                FilesResource.ListRequest listRequest = service.Files.List();
                listRequest.PageSize = 1000;
                listRequest.Fields = "nextPageToken, files(id, name, trashed, fullFileExtension, size)";
                if (result != null)
                    listRequest.PageToken = result.NextPageToken;

                try
                {
                    result = listRequest.Execute();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error calling Google API. Trying again.");
                    Console.WriteLine(e.Message);
                    Console.WriteLine();
                    retryNumber++;

                    if (retryNumber < 3)
                    {
                        Thread.Sleep(5000); // Pause 10 seconds
                        result = listRequest.Execute();
                    }
                    else
                    {
                        retryNumber = 0;
                        continue;
                    }

                }

                totalFileNumber = totalFileNumber + 1000;
                files.AddRange(result.Files);
                Console.WriteLine("Added 1000 files... Looking for more. Total Files: " + totalFileNumber.ToString());
            }

            //Persist List of Files
            try
            {
                Console.WriteLine(">------------ Begin Saving List to the Hard Disk <------------");
                WriteToJsonFile(string.Format(@location + "{0}", "ListOfFiles"), files, false);
                Console.WriteLine(">------------ End Saving List to the Hard Disk <------------");
            }
            catch { }


            Console.WriteLine( files.Count.ToString()  + " Total Files Found in GDrive.");
            Console.WriteLine("Date and Time: " + DateTime.Now);
            Console.WriteLine();

            bool trashflag = false;
            List<Google.Apis.Drive.v3.Data.File> trashfiles = new List<Google.Apis.Drive.v3.Data.File>();

            string[] lines = { };
            int filesizeMB = 0;

            List<String> filenameslist = new List<String>();

            try
            {
                if (files != null && files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        try
                        {
                            trashflag = (bool)file.Trashed;
                        }
                        catch
                        {
                            trashflag = false;
                        }

                        if (trashflag)
                        {
                            trashfiles.Add(file);
                            filesizeMB = 0; // int.Parse(file.Size.ToString()) / 1000;

                            Console.WriteLine("[TRASH - ] {0} ({1}), {2}, {3} Kb ({4} MB)", file.Name, file.Id, file.FileExtension, file.Size, filesizeMB);
                            filenameslist.Add(file.Name + "," + file.Id);

                            try
                            {
                                if (System.IO.File.Exists(string.Format(@location + "{0}", file.Name)))
                                {
                                    //Console.WriteLine("File exists locally...Skipped.");
                                }
                                else
                                {
                                    Console.WriteLine("File does not exist locally. Downloading.");
                                    //If it fails , try again once

                                    try
                                    {
                                        DownloadFile(service, file, string.Format(@location+ "{0}", file.Name));
                                    }
                                    catch (Exception e)
                                    {
                                        if (e.Message.Contains("Download failed."))
                                        {
                                            DownloadFile(service, file, string.Format(@location + "{0}", file.Name));
                                        }
                                        if (e.Message.Contains("binary"))
                                        {
                                            Console.WriteLine("Skipping Non-Binary File.");
                                        }

                                    }
                                }
                            }
                            catch
                            {
                                Console.WriteLine("Could not download {0}", file.Name);
                            }
                        }
                        else
                        {
                            //Console.WriteLine("{0} ({1}), {2}, {3}Kb", file.Name, file.Id, file.FileExtension, file.Size);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No files found.");
                }
            }
            catch { }

            Console.WriteLine();
            Console.WriteLine("Writing list of files in the trash to a text file.");
            System.IO.File.WriteAllLines(@location + "WriteLinesFinal.txt", filenameslist);

            Console.WriteLine();
            Console.WriteLine("Done. Press Any Key.");

            Console.Read();
            return 0;
        }



        private static void DownloadFile(Google.Apis.Drive.v3.DriveService service, Google.Apis.Drive.v3.Data.File file, string saveTo)
        {

            var request = service.Files.Get(file.Id);
            var stream = new System.IO.MemoryStream();

            // Add a handler which will be notified on progress changes.
            // It will notify on each chunk download and when the
            // download is completed or failed.
            request.MediaDownloader.ProgressChanged += (Google.Apis.Download.IDownloadProgress progress) =>
            {
                switch (progress.Status)
                {
                    case Google.Apis.Download.DownloadStatus.Downloading:
                        {
                            Console.WriteLine(progress.BytesDownloaded);
                            break;
                        }
                    case Google.Apis.Download.DownloadStatus.Completed:
                        {
                            Console.WriteLine("Download complete.");
                            SaveStream(stream, saveTo);
                            break;
                        }
                    case Google.Apis.Download.DownloadStatus.Failed:
                        {
                            Console.WriteLine("Download failed.");
                            break;
                        }
                }
            };
            request.Download(stream);

        }

        private static void SaveStream(System.IO.MemoryStream stream, string saveTo)
        {
            using (System.IO.FileStream file = new System.IO.FileStream(saveTo, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            {
                stream.WriteTo(file);
            }
        }





        /// <summary>
        /// Writes the given object instance to a binary file.
        /// <para>Object type (and all child types) must be decorated with the [Serializable] attribute.</para>
        /// <para>To prevent a variable from being serialized, decorate it with the [NonSerialized] attribute; cannot be applied to properties.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the binary file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the binary file.</param>
        /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
        public static void WriteToBinaryFile<T>(string filePath, T objectToWrite, bool append = false)
        {
            using (Stream stream = System.IO.File.Open(filePath, append ? FileMode.Append : FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);
            }
        }

        /// <summary>
        /// Reads an object instance from a binary file.
        /// </summary>
        /// <typeparam name="T">The type of object to read from the binary file.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the binary file.</returns>
        public static T ReadFromBinaryFile<T>(string filePath)
        {
            using (Stream stream = System.IO.File.Open(filePath, FileMode.Open))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (T)binaryFormatter.Deserialize(stream);
            }
        }


        /// <summary>
        /// Writes the given object instance to a Json file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// <para>Only Public properties and variables will be written to the file. These can be any type though, even other classes.</para>
        /// <para>If there are public properties/variables that you do not want written to the file, decorate them with the [JsonIgnore] attribute.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the file.</param>
        /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
        public static void WriteToJsonFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var contentsToWriteToFile = JsonConvert.SerializeObject(objectToWrite);
                writer = new StreamWriter(filePath, append);
                writer.Write(contentsToWriteToFile);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        /// <summary>
        /// Reads an object instance from an Json file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object to read from the file.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the Json file.</returns>
        public static T ReadFromJsonFile<T>(string filePath) where T : new()
        {
            TextReader reader = null;
            try
            {
                reader = new StreamReader(filePath);
                var fileContents = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<T>(fileContents);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        /// <summary>
        /// Writes the given object instance to an XML file.
        /// <para>Only Public properties and variables will be written to the file. These can be any type though, even other classes.</para>
        /// <para>If there are public properties/variables that you do not want written to the file, decorate them with the [XmlIgnore] attribute.</para>
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the file.</param>
        /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
        public static void WriteToXmlFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                writer = new StreamWriter(filePath, append);
                serializer.Serialize(writer, objectToWrite);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        /// <summary>
        /// Reads an object instance from an XML file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object to read from the file.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the XML file.</returns>
        public static T ReadFromXmlFile<T>(string filePath) where T : new()
        {
            TextReader reader = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                reader = new StreamReader(filePath);
                return (T)serializer.Deserialize(reader);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }


    }
}