using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Services;
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
        public async Task Run([ActivityTrigger] object input)
        {
            _logger.LogInformation("Transferring messages");

            var geo = await _geoService.GetGeoNamespace();
            await _geoService.TransferMessages(geo);

            _logger.LogInformation("Message transfer completed");

        }
    }
}
