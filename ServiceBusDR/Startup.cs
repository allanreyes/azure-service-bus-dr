using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using ServiceBusDR.Services;

[assembly: FunctionsStartup(typeof(ServiceBusDR.Startup))]


namespace ServiceBusDR
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton<IGeoService, GeoService>();
        }
    }
}
