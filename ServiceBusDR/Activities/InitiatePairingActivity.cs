using Microsoft.Azure.Management.ServiceBus.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class InitiatePairingActivity
    {
        private readonly IGeoService _geoService;

        public InitiatePairingActivity(IGeoService geoService)
        {
            _geoService = geoService;
        }

        [FunctionName(nameof(InitiatePairingActivity))]
        public async Task Run([ActivityTrigger] object input, ILogger logger)
        {
            var geo = await _geoService.GetGeoNamespace();

            var pairingStatus = await _geoService.GetPairingStatus(geo.Current.ResourceGroup, geo.Current.Name);
            var isAlreadyPaired = pairingStatus.Role != RoleDisasterRecovery.PrimaryNotReplicating;
            if (isAlreadyPaired)
            {
                logger.LogInformation("Already paired. No action performed.");
                return;
            }

            logger.LogInformation("Attempting to create pairing");

            var hasMessages = (await _geoService.GetNonEmptyEntities(geo)).Any();
            if (hasMessages)
            {
                var errorMessage = $"Cannot delete entities of '{geo.Partner.Name}' because some subscriptions contain messages. You have to run TransferMessages first.";
                logger.LogError(errorMessage);  
                throw new Exception(errorMessage);
            }

            await _geoService.DeleteAllEntities(geo);
           
            logger.LogInformation("Starting Pairing");
            var drStatus = await _geoService.InitiatePairing(geo);

            logger.LogInformation($"Waiting for pairing to complete");
            var status = drStatus.ProvisioningState;
            while (status != ProvisioningStateDR.Succeeded)
            {
                await Task.Delay(15000);
                status = (await _geoService.GetPairingStatus(geo.Current.ResourceGroup, geo.Current.Name)).ProvisioningState;
            }
            logger.LogInformation("Pairing successful");
        }
    }
}
