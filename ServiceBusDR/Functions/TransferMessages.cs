using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Services;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class TransferMessages
    {
        private readonly IGeoService _geoService;
        private readonly ILogger _logger;

        public TransferMessages(IGeoService geoService, ILogger<TransferMessages> logger)
        {
            _geoService = geoService;
            _logger = logger;
        }

        [FunctionName(nameof(TransferMessages))]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            string instanceId = await starter.StartNewAsync($"{nameof(TransferMessages)}Orchestrator", null);
            _logger.LogInformation($"Started TransferMessages orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName($"{nameof(TransferMessages)}Orchestrator")]
        public async Task RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(context.CallActivityAsync(nameof(TransferMessagesActivity), default));
            }
            await Task.WhenAll(tasks);
        }
    }
}
