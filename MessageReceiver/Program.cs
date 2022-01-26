using Azure.Messaging.ServiceBus;
using SharedConfig;

var client = new ServiceBusClient(Constants.GEO_LISTEN_CONNECTION);
await using var processor = client.CreateProcessor(Constants.TOPIC_NAME, Constants.SUBSCRIPTION_NAME, new ServiceBusProcessorOptions
{
    AutoCompleteMessages = false,
    MaxConcurrentCalls = 1,
});
processor.ProcessMessageAsync += MessageHandler;
processor.ProcessErrorAsync += ErrorHandler;

async Task MessageHandler(ProcessMessageEventArgs args)
{
    string body = args.Message.Body.ToString();
    Console.WriteLine(body);
    await args.CompleteMessageAsync(args.Message);
    await Task.Delay(new Random().Next(100, 1000));
}

Task ErrorHandler(ProcessErrorEventArgs args)
{
    Console.WriteLine(args.ErrorSource);
    Console.WriteLine(args.FullyQualifiedNamespace);
    Console.WriteLine(args.EntityPath);
    Console.WriteLine(args.Exception.ToString());
    return Task.CompletedTask;
}

await processor.StartProcessingAsync();
Console.WriteLine("Listening for messages");
Console.ReadKey();