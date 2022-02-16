using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Models;
using ServiceBusDR.Services;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class GetNonEmptyEntitiesActivity
    {
        private readonly IGeoService _geoService;

        public GetNonEmptyEntitiesActivity(IGeoService geoService)
        {
            _geoService = geoService;
        }

        [FunctionName(nameof(GetNonEmptyEntitiesActivity))]
        public async Task<string[]> Run([ActivityTrigger] GeoNamespace geo, ILogger logger)
        {
            logger.LogInformation("Checking if service bus has active messages");
            return await _geoService.GetNonEmptyEntities(geo);
        }
    }
}
