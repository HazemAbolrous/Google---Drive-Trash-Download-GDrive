using System;
using System.Collections.Generic;
using System.Threading;
using Google.Apis.Drive.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Apis.Services;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Upload;

public class GoogleDriveAccess
{
    public DriveService service { get; set; }
    public UserCredential credential { get; set; }

    public string[] Scopes = {DriveService.Scope.Drive,DriveService.Scope.DriveAppdata,DriveService.Scope.DriveMetadata,DriveService.Scope.DriveFile,DriveService.Scope.DriveMetadataReadonly, DriveService.Scope.DriveReadonly,DriveService.Scope.DriveScripts};
    public string ApplicationName = "Drive API .NET Quickstart";

    public string error { get; set; }

    public void LoadCredentials()
    {
        string user = "user-Rman";

        using (var stream = new System.IO.FileStream("client_secret.json", System.IO.FileMode.Open, System.IO.FileAccess.Read))
        {
            string credPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            credPath = System.IO.Path.Combine(credPath, ".credentials/drive-dotnet-quickstart.json");

               credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
               GoogleClientSecrets.Load(stream).Secrets,
               Scopes,
               user,
               CancellationToken.None,
               new FileDataStore(credPath, true)).Result;
        }
    }

    public void StartService()
    {
        service = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName 
        });
        service.HttpClient.Timeout = TimeSpan.FromMinutes(100);
    }

    public IList<File> GetFileList()
    {
        //// Define parameters of request.
        FilesResource.ListRequest listRequest = service.Files.List();
        listRequest.PageSize = 10;
        listRequest.Fields = "nextPageToken, files(id, name)";
        
        //// List files.
        IList<File> files = listRequest.Execute().Files;

        return files;
    }

    public void UploadFile(string filepath)
    {
        string filename = DateTime.Now.ToString().Replace("/","_").Replace(":","-") +"-" + System.IO.Path.GetFileName(filepath);

        var fileMetadata = new File()
        {
            Name = filename,
            MimeType = "application/unknown"
        };

        System.IO.FileStream fs = new System.IO.FileStream(filepath, System.IO.FileMode.Open);

        FilesResource.CreateMediaUpload uploadrequest = service.Files.Create(fileMetadata, fs, "application/unknown");
        uploadrequest.Fields = "id";

        //this variable can be used to track an errors while uploading
        IUploadProgress uploadProgress = uploadrequest.Upload();
        error = uploadProgress.Exception.Message;

        fs.Close();
        fs.Dispose();
    }

}

