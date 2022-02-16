using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Services;
using System.Net.Http;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class InitiatePairing
    {
        private readonly IGeoService _geoService;

        public InitiatePairing(IGeoService geoService)
        {
            _geoService = geoService;
        }

        [FunctionName(nameof(InitiatePairing))]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter, ILogger logger)
        {
            string instanceId = await starter.StartNewAsync($"{nameof(InitiatePairing)}Orchestrator", null);
            logger.LogInformation($"Started InitiatePairing orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName($"{nameof(InitiatePairing)}Orchestrator")]
        public async Task RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            await context.CallActivityAsync(nameof(InitiatePairingActivity), default);
        }
    }
}