using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VirtoCommerce.Storefront
{
    public class Program
    {
        public static void Main(string[] args)
        {            var host = CreateWebHostBuilder(args)
                .Build();

            using var scope = host.Services.CreateScope();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Store Front Program Running the host now..");
            host.Run();
        }

        public static IHostBuilder CreateWebHostBuilder(string[] args) =>
           Host.CreateDefaultBuilder(args)
              .UseContentRoot(Directory.GetCurrentDirectory())
              .ConfigureLogging((hostingContext, logging) =>
              {
                  logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                  logging.AddConsole();
                  logging.AddDebug();
                  // Enable Azure logging
                  // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-3.1#logging-in-azure
                  logging.AddAzureWebAppDiagnostics();

                  // Adding the filter below to ensure logs of all severity from Program.cs
                  // is sent to ApplicationInsights.
                  logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>
                      (typeof(Program).FullName, LogLevel.Trace);

                  // Adding the filter below to ensure logs of all severity from Startup.cs
                  // is sent to ApplicationInsights.
                  logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>
                      (typeof(Startup).FullName, LogLevel.Trace);
              })
              .ConfigureWebHostDefaults(webBuilder =>
              {
                  webBuilder.UseStartup<Startup>();
                  webBuilder.UseIISIntegration();
              });

    }

}
