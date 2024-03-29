﻿using Microsoft.Azure.Management.ServiceBus.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Services;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class FailoverActivity
    {
        private readonly IGeoService _geoService;

        public FailoverActivity(IGeoService geoService)
        {
            _geoService = geoService;
        }

        [FunctionName(nameof(FailoverActivity))]
        public async Task Run([ActivityTrigger] object input, ILogger logger)
        {
            logger.LogInformation("Executing failover");
            var geo = await _geoService.GetGeoNamespace();

            var pairingStatus = await _geoService.GetPairingStatus(geo.Current.ResourceGroup, geo.Current.Name);
            var isPaired = pairingStatus.Role != RoleDisasterRecovery.PrimaryNotReplicating;
            if (!isPaired)
            {
                logger.LogInformation("No pairing detected. Cannot failover.");
                return;
            }

            await _geoService.ExecuteFailover(geo);

            logger.LogInformation($"Waiting for Failover to complete.");
            pairingStatus = (await _geoService.GetPairingStatus(geo.Current.ResourceGroup, geo.Current.Name));
            while (pairingStatus.ProvisioningState != ProvisioningStateDR.Succeeded)
            {
                await Task.Delay(15000);
                // Noticee here we're checking the partner status instead
                pairingStatus = await _geoService.GetPairingStatus(geo.Partner.ResourceGroup, geo.Partner.Name);
            }
            logger.LogInformation("Failover completed");
        }
    }
}
