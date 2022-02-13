using System;
namespace ShoppingListServer.Services.Interfaces
{
    // Provides methods to access the file system and various file storage locations (in the docker volume).
    public interface IFilesystemService
    {
        string GetUserFolderPath(string user_id);

        // Create the folder where user shoppinglists stored in.
        bool CreateUserFolder(string user_id);

        // Returns True when folder exist after method run.
        bool CreateFolder(string folder_path);
    }
}
