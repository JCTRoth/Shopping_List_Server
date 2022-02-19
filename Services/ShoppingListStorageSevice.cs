using System;
using System.IO;
using Newtonsoft.Json;
using ShoppingListServer.Models;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Services
{
    public class ShoppingListStorageSevice : IShoppingListStorageService
    {
        IFilesystemService _filesystemService;

        public ShoppingListStorageSevice(IFilesystemService filesystemService)
        {
            _filesystemService = filesystemService;
        }

        public ShoppingList Load_ShoppingList(string user_id, string shoppingList_id)
        {
            ShoppingList list = null;
            try
            {
                string file_path =
                    System.IO.Path.Combine(_filesystemService.GetUserFolderPath(user_id), shoppingList_id + ".json");

                if (File.Exists(file_path))
                {
                    string file_content = File.ReadAllText(file_path);
                    list = JsonConvert.DeserializeObject<ShoppingList>(file_content);
                }
                else
                {
                    Console.Error.WriteLine("Load_ShoppingList: list not found at " + file_path);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Load_ShoppingList " + ex);
            }
            return list;
        }

        public bool Store_ShoppingList(string user_id, ShoppingList shoppingList)
        {
            try
            {
                string folder_path = _filesystemService.GetUserFolderPath(user_id);
                string file_path = System.IO.Path.Combine(folder_path, shoppingList.SyncId + ".json");
                string list_as_string = JsonConvert.SerializeObject(shoppingList);

                if (!System.IO.Directory.Exists(folder_path))
                {
                    System.IO.Directory.CreateDirectory(folder_path);
                }

                File.WriteAllText(file_path, list_as_string);

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Store_ShoppingList " + ex);
                return false;
            }
        }

        public bool Update_ShoppingList(string user_id, ShoppingList shoppingList)
        {
            try
            {
                string folder_path = _filesystemService.GetUserFolderPath(user_id);
                string file_path = System.IO.Path.Combine(folder_path, shoppingList.SyncId + ".json");
                string list_as_string = JsonConvert.SerializeObject(shoppingList);

                if (!System.IO.Directory.Exists(folder_path))
                {
                    return false;
                }

                if (!System.IO.File.Exists(file_path))
                {
                    return false;
                }

                File.WriteAllText(file_path, list_as_string);

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Update_ShoppingList " + ex);
                return false;
            }
        }

        public bool Move_ShoppingList(string user_id_old, string user_id_new, string shoppingList_id)
        {
            try
            {
                string file_path_source =
                    System.IO.Path.Combine(_filesystemService.GetUserFolderPath(user_id_old), shoppingList_id + ".json");
                string file_path_dest =
                    System.IO.Path.Combine(_filesystemService.GetUserFolderPath(user_id_new), shoppingList_id + ".json");

                if (File.Exists(file_path_source))
                {
                    File.Move(file_path_source, file_path_dest);
                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Delete_ShoppingList " + ex);
                return false;
            }
        }

        public bool Delete_ShoppingList(string user_id, string shoppingList_id)
        {
            try
            {
                string file_path =
                    System.IO.Path.Combine(_filesystemService.GetUserFolderPath(user_id), shoppingList_id + ".json");

                if (File.Exists(file_path))
                {
                    File.Delete(file_path);
                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Delete_ShoppingList " + ex);
                return false;
            }
        }
    }
}
