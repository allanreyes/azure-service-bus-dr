using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ServiceBusDR.Models;
using ServiceBusDR.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class TransferMessages
    {
        private readonly IGeoService _geoService;

        public TransferMessages(IGeoService geoService)
        {
            _geoService = geoService;
        }

        [FunctionName(nameof(TransferMessages))]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter, ILogger logger)
        {
            string instanceId = await starter.StartNewAsync($"{nameof(TransferMessages)}Orchestrator", null);
            logger.LogInformation($"Started TransferMessages orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName($"{nameof(TransferMessages)}Orchestrator")]
        public async Task RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            logger.LogInformation($"{nameof(TransferMessages)} Started. InstanceId: {context.InstanceId}");
            var geo = await context.CallActivityAsync<GeoNamespace>(nameof(GetGeoNamespaceActivity), default);
            var paths = await context.CallActivityAsync<string[]>(nameof(GetNonEmptyEntitiesActivity), geo);

            while (paths.Length > 0)
            {
                logger.LogInformation($"Found non-empty entities: {JsonConvert.SerializeObject(paths)}");
                var tasks = new List<Task>(); // Fan-out
                foreach (var path in paths)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        tasks.Add(context.CallActivityAsync(nameof(TransferMessagesActivity), (geo, path)));
                    }
                }
                await Task.WhenAll(tasks);
                await Task.Delay(TimeSpan.FromSeconds(5)); // Wait a little for the counts to be updated
                paths = await context.CallActivityAsync<string[]>(nameof(GetNonEmptyEntitiesActivity), geo);
            }
            logger.LogInformation($"{nameof(TransferMessages)} Completed. InstanceId: {context.InstanceId}");
        }
    }
}
