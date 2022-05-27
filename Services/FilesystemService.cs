using System;
using Microsoft.Extensions.Options;
using ShoppingListServer.Helpers;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Services
{
    public class FilesystemService : IFilesystemService
    {
        public string DataStoragePath { get; set; } // default: data
        public string UserStoragePath { get; set; } // default: user

        public FilesystemService(IOptions<AppSettings> appSettings)
        {
            DataStoragePath = appSettings.Value.DataStorageFolder; // /<DataStorageFolder>
            UserStoragePath = System.IO.Path.Combine(appSettings.Value.DataStorageFolder, appSettings.Value.UserStorageFolder); // /<DataStorageFolder>/<UserFolder>

            // Create APIs storage folder
            CreateStorageFolders();
        }

        public string GetUserFolderPath(string user_id)
        {
            return System.IO.Path.Combine(UserStoragePath, user_id);
        }

        // Create the folder where user shoppinglists stored in.
        public bool CreateUserFolder(string user_id)
        {
            return CreateFolder(GetUserFolderPath(user_id));
        }

        // Returns True when folder exist after method run.
        public bool CreateFolder(string folder_path)
        {
            try
            {
                if (!System.IO.Directory.Exists(folder_path))
                {
                    System.IO.Directory.CreateDirectory(folder_path);

                    if (System.IO.Directory.Exists(folder_path))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Create_Data_Storage_Folder " + ex);
            }

            return false;
        }

        // Creates the Data Storage Folder where JSON Shopping List's are placed in
        // After Creation set's config variable
        private bool CreateStorageFolders()
        {
            bool success = CreateFolder(DataStoragePath);
            success = CreateFolder(UserStoragePath);
            return success;
        }
    }
}
