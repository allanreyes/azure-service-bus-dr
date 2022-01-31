using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using ServiceBusDR.Services;
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
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var geo = await _geoService.GetGeoNamespace();
            await _geoService.TransferMessages(geo);

            return new OkResult();
        }
    }
}
