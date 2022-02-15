using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ServiceBusDR.Services;

[assembly: FunctionsStartup(typeof(ServiceBusDR.Startup))]


namespace ServiceBusDR
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var logger = new LoggerConfiguration()
                         .WriteTo.Console()
                         .CreateLogger();
            builder.Services.AddLogging(lb => lb.AddSerilog(logger));
            builder.Services.AddSingleton<IGeoService, GeoService>();
        }
    }
}
