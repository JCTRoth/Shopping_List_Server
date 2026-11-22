using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Linq;

namespace ShoppingListServer
{
    public class Program
    {

        public static void Main(string[] args)
        {
            // UseContentRoot: Use the current directory as root so that the wwwroot folder is a child. (not sure if it's necessary to call this)
            CreateHostBuilder(args)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .Build().Run();
        }

        // Uses the certificates specified in
        // Calls Configure which sets up Kestrel, see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-6.0
        // "Kestrel" -> "Certificates" -> "Default"
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    // Host.CreateDefaultBuilder registers appsettings.json with
                    // reloadOnChange: true, which starts an inotify FileSystemWatcher.
                    // Containers inherit the host's fs.inotify.max_user_instances limit
                    // (128 by default on the k3s node), shared by every pod, and running
                    // out of instances aborts startup with:
                    //   "The configured user limit (128) on the number of inotify
                    //    instances has been reached"
                    // Configuration is only read at startup in the container, so turn
                    // file watching off. This delegate runs after the default sources
                    // have been added, so they can still be adjusted here.
                    foreach (var source in config.Sources.OfType<FileConfigurationSource>())
                    {
                        source.ReloadOnChange = false;
                    }

                    config.AddJsonFile("auth/secrets.json", optional: true, reloadOnChange: false);
                    if (hostingContext.HostingEnvironment.IsDevelopment())
                    {
                        config.AddJsonFile("auth/devMode.json", optional: true, reloadOnChange: false);
                        config.AddJsonFile("auth/dev/credentials.json", optional: true, reloadOnChange: false);
                    }
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                        .UseUrls("http://0.0.0.0:5677"); // The http port 5677 is only needed for local server with iPhone emulator
                });
    }
}
