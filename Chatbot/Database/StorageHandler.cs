using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace Chatbot.Database
{
    [Obsolete]
    public class StorageHandler
    {
        private readonly CloudBlobClient _serviceClient;

        public StorageHandler(string connectionString)
        {
            var account = CloudStorageAccount.Parse(connectionString);
            _serviceClient = account.CreateCloudBlobClient();
        }

        public void WriteUserToTextFile(UserInDb user)
        {
            try
            {
                var uniqueName = user.UniqueName.Replace("@", "at");
                uniqueName = uniqueName.Replace(".", "dot");
                var container = _serviceClient.GetContainerReference("userdatacontainer");
                container.CreateIfNotExistsAsync().Wait();
                CloudBlockBlob blob = container.GetBlockBlobReference(uniqueName + ".json");
                blob.UploadTextAsync(JsonConvert.SerializeObject(user));
            }
            catch
            {
                // ignored
            }
        }

        public UserInDb ReadUserFromStorage(string uniqueName)
        {
            try
            {
                uniqueName = uniqueName.Replace("@", "at");
                uniqueName = uniqueName.Replace(".", "dot");
                var container = _serviceClient.GetContainerReference("userdatacontainer");
                CloudBlockBlob blob = container.GetBlockBlobReference(uniqueName + ".json");
                var json = blob.DownloadTextAsync().Result;
                var user = JsonConvert.DeserializeObject<UserInDb>(json);
                return user;
            }
            catch
            {
                return new UserInDb()
                {
                    UniqueName = uniqueName
                };
            }
        }

    }
}
