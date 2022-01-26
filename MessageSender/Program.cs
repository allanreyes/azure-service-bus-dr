using Azure.Messaging.ServiceBus;
using SharedConfig;

var client = new ServiceBusClient(Constants.GEO_SEND_CONNECTION);
var sender = client.CreateSender(Constants.TOPIC_NAME);

while (true)
{
    Console.WriteLine("How many messages to create?");

    var batch = Guid.NewGuid().ToString().Substring(0, 5);
    int count;
    if (int.TryParse(Console.ReadLine(), out count))
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

