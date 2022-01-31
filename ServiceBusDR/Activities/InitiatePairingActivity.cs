using Microsoft.Azure.Management.ServiceBus.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Services;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class InitiatePairingActivity
    {
        private readonly IGeoService _geoService;
        private readonly ILogger _logger;

        public InitiatePairingActivity(IGeoService geoService, ILogger<InitiatePairingActivity> logger)
        {
            _geoService = geoService;
            _logger = logger;
        }

        [FunctionName(nameof(InitiatePairingActivity))]
        public async Task Run([ActivityTrigger] object input)
        {
            var geo = await _geoService.GetGeoNamespace();

            var pairingStatus = await _geoService.GetPairingStatus(geo.Current.ResourceGroup, geo.Current.Name);
            var isAlreadyPaired = pairingStatus.Role != RoleDisasterRecovery.PrimaryNotReplicating;
            if (isAlreadyPaired)
            {
                _logger.LogInformation("Already paired. No action performed.");
                return;
            }

            _logger.LogInformation("Attempting to create pairing");

            await _geoService.TransferMessages(geo);

            var isEmpty = await _geoService.DeleteAllEntities(geo);
            while (!isEmpty)
            {
                _logger.LogInformation($"Cannot delete entities of '{geo.Partner.Name}' because some subscriptions contain messages");
                _logger.LogInformation($"Transfering messages");
                await _geoService.TransferMessages(geo);
                isEmpty = await _geoService.DeleteAllEntities(geo);
            }

            _logger.LogInformation("Starting Pairing");
            var drStatus = await _geoService.InitiatePairing(geo);

            _logger.LogInformation($"Waiting for pairing to complete");
            var status = drStatus.ProvisioningState;
            while (status != ProvisioningStateDR.Succeeded)
            {
                await Task.Delay(15000);
                status = (await _geoService.GetPairingStatus(geo.Current.ResourceGroup, geo.Current.Name)).ProvisioningState;
            }

            _logger.LogInformation("Pairing successful");
        }
    }
}
