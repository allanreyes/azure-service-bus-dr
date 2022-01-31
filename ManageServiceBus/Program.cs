using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Azure.Management.ServiceBus;
using Microsoft.Azure.Management.ServiceBus.Models;
using Microsoft.Rest;
using SharedConfig;

// In Visual Studio, go to Tools > Options > Azure Service Authentication > Account Selection
// to make sure you're using the correct Azure account
var cred = new DefaultAzureCredential();
var adminClient = new ServiceBusAdministrationClient(Constants.ALIAS.ToFQNS(), cred);
var managementClient = new ServiceBusManagementClient(await GetTokenCredentials(cred));
managementClient.SubscriptionId = Constants.AZURE_SUBSCRIPTION_ID;

var namespaces = await managementClient.Namespaces.ListAsync();

var sc = StringComparison.InvariantCultureIgnoreCase;

Console.WriteLine("Choose an action:");
Console.WriteLine("[A] Get Geo Status");
Console.WriteLine("[B] Initiate Pairing");
Console.WriteLine("[C] Failover");
Console.WriteLine("[D] Failback");
Console.WriteLine("[Any other key] Count messages");

while (true)
{
    var key = Console.ReadKey(true).KeyChar;
    var keyPressed = key.ToString().ToUpper();
    Console.WriteLine($"{new string('-', 10)} {DateTime.Now:yyyy/MM/dd HH:mm:ss} {new string('-', 10)}");

    switch (keyPressed)
    {
        case "A":
            await GetGeoRecoveryStatus();
            break;
        case "B":
            await CreatePairing();
            break;
        case "C":
            await ExecuteFailover();
            break;
        case "D":
            await ExecuteFailback();
            break;
        default:
            break;
    }
}

async Task<ArmDisasterRecovery> GetGeoRecoveryStatus()
{
    var geo = await GetGeoNamespace();
    var pairingStatus = await GetPairingStatus(geo.CurrentResourceGroup, geo.CurrentNameSpace);
    Console.WriteLine($"Alias               : {pairingStatus.Name}");
    Console.WriteLine($"Provisioning State  : {pairingStatus.ProvisioningState}");
    Console.WriteLine($"Primary / Role      : {pairingStatus.Id.Split('/')[8]} / {pairingStatus.Role}");

    var isPaired = !string.IsNullOrEmpty(pairingStatus.PartnerNamespace);
    if (isPaired)
    {
        Console.WriteLine($"Secondary           : {pairingStatus.PartnerNamespace.Split('/').Last()}");
    }
    else
    {
        Console.WriteLine($"Secondary           : --");
    }
    return pairingStatus;
}

async Task CreatePairing()
{
    Console.WriteLine("Attempting to create pairing");
    var pairingStatus = await GetGeoRecoveryStatus();

    var isPaired = pairingStatus.Role != RoleDisasterRecovery.PrimaryNotReplicating;
    if (isPaired)
    {
        Console.WriteLine("Already paired. No action performed.");
        return;
    }

    var geo = await GetGeoNamespace();
    Console.WriteLine($"Geo namespace is currently pointing to  : {geo.CurrentNameSpace}");
    Console.WriteLine($"Adding secondary namespace              : {geo.PartnerNameSpace}");

    await TransferMessages(geo);

    var isEmpty = await DeleteAllEntities(geo);
    while (!isEmpty)
    {
        Console.WriteLine($"Cannot delete entities of '{geo.PartnerNameSpace}' because some subscriptions contain messages");
        Console.WriteLine($"Transfering messages");
        await TransferMessages(geo);
        isEmpty = await DeleteAllEntities(geo);
    }

    Console.WriteLine("Starting Pairing");
    var drStatus = await managementClient.DisasterRecoveryConfigs.CreateOrUpdateAsync(
            geo.CurrentResourceGroup,
            namespaceName: geo.CurrentNameSpace,
            alias: Constants.ALIAS,
            new ArmDisasterRecovery() { PartnerNamespace = geo.PartnerNameSpaceId });

    Console.WriteLine($"Waiting for pairing to complete");
    var status = drStatus.ProvisioningState;
    while (status != ProvisioningStateDR.Succeeded)
    {
        await Task.Delay(15000);
        status = (await GetPairingStatus(geo.CurrentResourceGroup, geo.CurrentNameSpace)).ProvisioningState;
        Console.Write(".");
    }

    Console.WriteLine("Pairing successful");
}

async Task ExecuteFailover()
{
    Console.WriteLine("Executing failover");
    var geo = await GetGeoNamespace();
    var pairingStatus = await GetPairingStatus(geo.CurrentResourceGroup, geo.CurrentNameSpace);
    var isPaired = !string.IsNullOrEmpty(pairingStatus.PartnerNamespace);
    if (!isPaired)
    {
        Console.WriteLine($"No pairing detected. Cannot failover.");
    }
    else
    {
        await managementClient.DisasterRecoveryConfigs.FailOverAsync(geo.PartnerResourceGroup, geo.PartnerNameSpace, alias: Constants.ALIAS);

        Console.WriteLine($"Waiting for Failover to complete.");
        pairingStatus = (await GetPairingStatus(geo.CurrentResourceGroup, geo.CurrentNameSpace));
        while (pairingStatus.ProvisioningState != ProvisioningStateDR.Succeeded)
        {
            await Task.Delay(15000);
            // Noticee here we're checking the partner status instead
            pairingStatus = await GetPairingStatus(geo.PartnerResourceGroup, geo.PartnerNameSpace);
            Console.Write(".");
        }
        Console.WriteLine("Failover completed");
    }
}

async Task ExecuteFailback()
{
    Console.WriteLine("Failing back to primary namespace");
    await CreatePairing(); // Pair with primary
    await ExecuteFailover(); // Point alias to primary and drop secondary
    await CreatePairing(); // Pair with secondary
    Console.WriteLine("Failing back completed");
}

async Task TransferMessages(GeoNamespace geo)
{
    await using var senderClient = new ServiceBusClient($"{geo.CurrentNameSpace}.servicebus.windows.net", credential: cred);
    await using var receiverClient = new ServiceBusClient($"{geo.PartnerNameSpace}.servicebus.windows.net", credential: cred);
    var adminClient = new ServiceBusAdministrationClient($"{geo.PartnerNameSpace}.servicebus.windows.net", credential: cred);

    var topics = await managementClient.Topics.ListByNamespaceAsync(geo.PartnerResourceGroup, geo.PartnerNameSpace);

    foreach (var topic in topics)
    {
        var sender = senderClient.CreateSender(topic.Name);

        var subscriptions = await managementClient.Subscriptions.ListByTopicAsync(geo.PartnerResourceGroup, geo.PartnerNameSpace, topic.Name);
        foreach (var subscription in subscriptions)
        {
            var subProperties = await adminClient.GetSubscriptionRuntimePropertiesAsync(topic.Name, subscription.Name);
            var activeMessageCount = subProperties.Value.ActiveMessageCount;
            if (activeMessageCount > 0)
            {
                var receiver = receiverClient.CreateReceiver(topic.Name, subscription.Name);
                var messages = await receiver.ReceiveMessagesAsync(int.MaxValue);
                Console.WriteLine($"Received {messages.Count} messages from source subscription '{subscription.Name}' under topic '{topic.Name}' ");
                foreach (var message in messages)
                {
                    await sender.SendMessageAsync(new ServiceBusMessage(message.Body));
                    await receiver.CompleteMessageAsync(message);
                }
                Console.WriteLine($"Copied {messages.Count} messages from source subscription '{subscription.Name}' to target topic '{topic.Name}' ");
            }
        }
    }
}

async Task<TokenCredentials> GetTokenCredentials(DefaultAzureCredential cred)
{
    var ctx = new TokenRequestContext(scopes: new[] { "https://management.azure.com/.default" });
    var token = await cred.GetTokenAsync(ctx, default);
    return new TokenCredentials(token.Token);
}

async Task<ArmDisasterRecovery> GetPairingStatus(string resourceGroup, string namespaceName)
{
    return await managementClient.DisasterRecoveryConfigs.GetAsync(
        resourceGroup,
        namespaceName,
        alias: Constants.ALIAS);
}

async Task<bool> DeleteAllEntities(GeoNamespace geo)
{
    Console.WriteLine("Clearing entities in secondary namespace");
    var adminClient = new ServiceBusAdministrationClient($"{geo.PartnerNameSpace}.servicebus.windows.net", credential: cred);

    var topicsToDelete = await managementClient.Topics.ListByNamespaceAsync(geo.PartnerResourceGroup, geo.PartnerNameSpace);
    bool hasMessages = false;
    foreach (var topic in topicsToDelete)
    {
        var subscriptions = await managementClient.Subscriptions.ListByTopicAsync(geo.PartnerResourceGroup, geo.PartnerNameSpace, topic.Name);
        foreach (var subscription in subscriptions)
        {
            var subProperties = await adminClient.GetSubscriptionRuntimePropertiesAsync(topic.Name, subscription.Name);
            var activeMessageCount = subProperties.Value.ActiveMessageCount;
            if (activeMessageCount > 0)
            {
                Console.WriteLine($"Topic '{topic.Name}' Subscription '{subscription.Name}' has {activeMessageCount} active messages");
                hasMessages = true;
            }
        }
    }

    if (hasMessages) return false;

    foreach (var topic in topicsToDelete)
        await managementClient.Topics.DeleteAsync(geo.PartnerResourceGroup, geo.PartnerNameSpace, topic.Name);

    return true;
}

async Task<GeoNamespace> GetGeoNamespace()
{
    var ns = await adminClient.GetNamespacePropertiesAsync();
    if (!ns.Value.Alias.Equals(Constants.ALIAS, sc))
        throw new Exception("Alias in configuration does not match alias in this Service Bus namespace.");

    var current = ns.Value.Name;
    var isUsingPrimary = current.Equals(Constants.PRIMARY_NAMESPACE, sc);
    var partner = isUsingPrimary ? Constants.SECONDARY_NAMESPACE : Constants.PRIMARY_NAMESPACE;

    var geoNs = new GeoNamespace()
    {
        CurrentNameSpace = current,
        PartnerNameSpace = partner
    };

    var currentNs = namespaces?.SingleOrDefault(n => n.Name.Equals(current, sc));
    geoNs.CurrentNameSpaceId = currentNs?.Id;
    geoNs.CurrentResourceGroup = currentNs?.Id.Split('/')[4];

    var partnerNs = namespaces?.SingleOrDefault(n => n.Name.Equals(partner, sc));
    geoNs.PartnerNameSpaceId = partnerNs?.Id;
    geoNs.PartnerResourceGroup = partnerNs?.Id.Split('/')[4];

    return geoNs;
}

class GeoNamespace
{
    public string? CurrentNameSpace { get; set; }
    public string? CurrentNameSpaceId { get; set; }
    public string? CurrentResourceGroup { get; set; }
    public string? PartnerNameSpace { get; set; }
    public string? PartnerNameSpaceId { get; set; }
    public string? PartnerResourceGroup { get; set; }
}