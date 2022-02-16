using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.ServiceBus.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Services;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class GetGeoStatus
    {
        private readonly IGeoService _geoService;

        public GetGeoStatus(IGeoService geoService)
        {
            _geoService = geoService;
        }

        [FunctionName(nameof(GetGeoStatus))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger logger)
        {
            logger.LogInformation($"Triggered {nameof(GetGeoStatus)}");
            var geo = await _geoService.GetGeoNamespace();
            var pairingStatus = await _geoService.GetPairingStatus(geo.Current.ResourceGroup, geo.Current.Name);

            var response = new
            {
                Alias = pairingStatus.Name,
                ProvisioningState = pairingStatus.ProvisioningState,
                Role = pairingStatus.Role,
                geo.Current,
                Partner = pairingStatus.Role != RoleDisasterRecovery.PrimaryNotReplicating ? geo.Partner : null
            };

            return new OkObjectResult(response);
        }
    }
}
