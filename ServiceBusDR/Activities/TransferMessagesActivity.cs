using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Models;
using ServiceBusDR.Services;
using System;
using System.Threading.Tasks;

namespace ServiceBusDR
{
    public class TransferMessagesActivity
    {
        private readonly IGeoService _geoService;
        private readonly ILogger _logger;

        public TransferMessagesActivity(IGeoService geoService, ILogger<TransferMessagesActivity> logger)
        {
            _geoService = geoService;
            _logger = logger;
        }

        [FunctionName(nameof(TransferMessagesActivity))]
        public async Task Run([ActivityTrigger] Tuple<GeoNamespace, string> input)
        {
            _logger.LogInformation("Transferring messages");
            await _geoService.TransferMessages(input.Item1, input.Item2);
            _logger.LogInformation("Message transfer completed");
        }
    }
}
