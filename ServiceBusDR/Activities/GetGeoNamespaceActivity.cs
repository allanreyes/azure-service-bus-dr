using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Models;
using ServiceBusDR.Services;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class GetGeoNamespaceActivity
    {
        private readonly IGeoService _geoService;

        public GetGeoNamespaceActivity(IGeoService geoService)
        {
            _geoService = geoService;
        }

        [FunctionName(nameof(GetGeoNamespaceActivity))]
        public async Task<GeoNamespace> Run([ActivityTrigger] object input, ILogger logger)
        {
            logger.LogInformation("Getting GeoNamespace");
            return await _geoService.GetGeoNamespace();
        }
    }
}
