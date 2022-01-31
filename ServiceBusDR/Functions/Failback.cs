using Microsoft.Azure.Management.ServiceBus.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Services;
using System.Net.Http;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class Failback
    {
        private readonly IGeoService _geoService;
        private readonly ILogger _logger;

        public Failback(IGeoService geoService, ILogger<Failback> logger)
        {
            _geoService = geoService;
            _logger = logger;
        }

        [FunctionName(nameof(Failback))]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            string instanceId = await starter.StartNewAsync($"{nameof(Failback)}Orchestrator", null);
            _logger.LogInformation($"Started Failback orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName($"{nameof(Failback)}Orchestrator")]
        public async Task RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            _logger.LogInformation("Executing failback");

            _logger.LogInformation("Failing back to primary namespace");
            await context.CallActivityAsync(nameof(InitiatePairingActivity), default);

            _logger.LogInformation("Point alias to primary and drop secondary");
            await context.CallActivityAsync(nameof(FailoverActivity), default);

            _logger.LogInformation("Pairing with secondary");
            await context.CallActivityAsync(nameof(InitiatePairingActivity), default);

        }
    }
}