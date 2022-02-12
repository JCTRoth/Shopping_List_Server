using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ShoppingListServer.Logic;
using ShoppingListServer.Models;

namespace ShoppingListServer
{
    public class Program
    {
        // TODO Replace Config by Config Service
        public static string _data_storage_folder;

        public static void Main(string[] args)
        {
            // Create APIs storage folder
            new Folder().Create_Data_Storage_Folder();
            Console.WriteLine("Data Storage Folder: {0}", _data_storage_folder);
            CreateHostBuilder(args).Build().Run();
        }

        // Uses the certificates specified in
        // Calls Configure which sets up Kestrel, see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-6.0
        // "Kestrel" -> "Certificates" -> "Default"
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                        .UseSetting("https_port", "5678")
                        .UseUrls("https://0.0.0.0:5678;http://0.0.0.0:5677"); // The http port 5677 is only needed for local server with iPhone emulator
                });
    }
}
