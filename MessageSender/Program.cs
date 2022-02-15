using Azure.Identity;
using Azure.Messaging.ServiceBus;
using ServiceBusDR.Services;
using SharedConfig;

var cred = new DefaultAzureCredential();
var client = new ServiceBusClient(Constants.ALIAS.ToFQNS(), credential: cred);
var sender = client.CreateSender(Constants.TOPIC_NAME);

while (true)
{
    Console.WriteLine("How many messages to create?");

    var batch = DateTime.Now.ToString("HHmmss");
    if (int.TryParse(Console.ReadLine(), out int count))
    {
        for (int i = 0; i < count; i++)
        {
            var message = $"{batch} - message { i + 1 } of { count }";
            Console.WriteLine(message);
            await sender.SendMessageAsync(new ServiceBusMessage(message));
            await Task.Delay(new Random().Next(100, 1000));
        }
    }
}


