using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.IO;

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
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                        .UseSetting("https_port", "5678")
                        .UseUrls("https://0.0.0.0:5678;http://0.0.0.0:5677"); // The http port 5677 is only needed for local server with iPhone emulator
                });
    }
}
