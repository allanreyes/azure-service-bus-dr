using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Services;
using System.Net.Http;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class Failover
    {
        private readonly IGeoService _geoService;

        public Failover(IGeoService geoService)
        {
            _geoService = geoService;
        }

        [FunctionName(nameof(Failover))]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter, ILogger logger)
        {
            string instanceId = await starter.StartNewAsync($"{nameof(Failover)}Orchestrator", null);
            logger.LogInformation($"Started Failover orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName($"{nameof(Failover)}Orchestrator")]
        public async Task RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            await context.CallActivityAsync(nameof(FailoverActivity), default);
        }
    }
}