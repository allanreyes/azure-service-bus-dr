using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Dapper;
using SharedConfig;
using System.Data.SqlClient;

var cred = new DefaultAzureCredential();
var client = new ServiceBusClient(Constants.ALIAS.ToFQNS(), credential: cred);

var conn = @"Server=(LocalDB)\MSSQLLocalDB;Integrated Security=true;database=ServiceBusDRTest";
string sqlCustomerInsert = "INSERT INTO MessageLog (batch) Values (@Batch);";

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
    using (var connection = new SqlConnection(conn))
    {
        var affectedRows = connection.Execute(sqlCustomerInsert, new { Batch = body.Substring(0, 5) });
    }
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