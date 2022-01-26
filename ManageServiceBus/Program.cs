using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Azure.Management.ServiceBus;
using Microsoft.Azure.Management.ServiceBus.Models;
using Microsoft.Rest;
using SharedConfig;

var adminClient = new ServiceBusAdministrationClient(Constants.GEO_ROOT_MANAGE_CONNECTION);
// In Visual Studio, go to Tools > Options > Azure Service Authentication > Account Selection
// to make sure you're using the correct Azure account
var managementClient = new ServiceBusManagementClient(await GetTokenCredentials())
{ SubscriptionId = Constants.AZURE_SUBSCRIPTION_ID };
var clientPrimary = new ServiceBusAdministrationClient(Constants.PRIMARY_ROOT_MANAGE_CONNECTION);
var clientSecondary = new ServiceBusAdministrationClient(Constants.SECONDARY_ROOT_MANAGE_CONNECTION);
var namespaces = await managementClient.Namespaces.ListAsync();

var sc = StringComparison.InvariantCultureIgnoreCase;

Console.WriteLine("Choose an action:");
Console.WriteLine("[A] Initiate Pairing");
Console.WriteLine("[B] Failover");
Console.WriteLine("[C] Break pairing");
Console.WriteLine("[Any other key] Count messages");

while (true)
{
    var key = Console.ReadKey(true).KeyChar;
    var keyPressed = key.ToString().ToUpper();

    switch (keyPressed)
    {
        case "A":
            await CreatePairing();
            break;
        case "B":
            await ExecuteFailover();
            break;
        case "C":
            await BreakPairing();
            break;
        default:
            await CountMessages();
            break;
    }
}

async Task CreatePairing()
{
    Console.WriteLine("Attempting to create pairing");
    var geo = await GetGeoNamespace();

    Console.WriteLine($"Geo namespace is currently pointing to: {geo.CurrentNameSpace}");
    Console.WriteLine($"Adding secondary namespace: {geo.PartnerNameSpace}");

    if (await IsPaired(geo.CurrentResourceGroup, geo.CurrentNameSpace))
    {
        Console.WriteLine("Already paired. No action performed.");
    }
    else
    {
        // TODO: Should check and transfer messages first
        // Partner namespace needs to have no entities before it can be paired
        await DeleteAllEntities(geo.PartnerResourceGroup, geo.PartnerNameSpace);

        Console.WriteLine("Starting Pairing");
        var drStatus = await managementClient.DisasterRecoveryConfigs.CreateOrUpdateAsync(
                geo.CurrentResourceGroup,
                namespaceName: geo.CurrentNameSpace,
                alias: Constants.ALIAS,
                new ArmDisasterRecovery() { PartnerNamespace = geo.PartnerNameSpaceId });

        while (drStatus.ProvisioningState != ProvisioningStateDR.Succeeded)
        {
            Console.WriteLine("Waiting for DR to be setup. Current State: " + drStatus.ProvisioningState);
            await Task.Delay(10000);

            drStatus = await managementClient.DisasterRecoveryConfigs.GetAsync(
                geo.CurrentResourceGroup,
                namespaceName: geo.CurrentNameSpace,
                alias: Constants.ALIAS);
        }
        Console.WriteLine("Pairing successful");
    }
}

async Task ExecuteFailover()
{
    Console.WriteLine("Executing failover");
    var geo = await GetGeoNamespace();
    await managementClient.DisasterRecoveryConfigs.FailOverAsync(geo.PartnerResourceGroup, geo.PartnerNameSpace, alias: Constants.ALIAS);
    Console.WriteLine("Failover started");
    // TODO: Find a way to get status
}

async Task BreakPairing()
{
    Console.WriteLine("Breaking pairing");
    var geo = await GetGeoNamespace();
    await managementClient.DisasterRecoveryConfigs.BreakPairingAsync(geo.CurrentResourceGroup, geo.CurrentNameSpace, alias: Constants.ALIAS);
    Console.WriteLine("Break pairing started");
    // TODO: Find a way to get status
}

async Task CountMessages()
{
    var timeStamp = $"{new string('-', 10)}{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}{new string('-', 10)}";
    var primary = await clientPrimary.GetSubscriptionRuntimePropertiesAsync(Constants.TOPIC_NAME, Constants.SUBSCRIPTION_NAME);
    var secondary = await clientSecondary.GetSubscriptionRuntimePropertiesAsync(Constants.TOPIC_NAME, Constants.SUBSCRIPTION_NAME);
    Console.WriteLine(timeStamp);
    Console.WriteLine($"Canada East (Primary)       : {primary.Value.ActiveMessageCount}");
    Console.WriteLine($"Canada Central (Secondary)  : {secondary.Value.ActiveMessageCount}");
}

async Task<TokenCredentials> GetTokenCredentials()
{
    var cred = new DefaultAzureCredential(new DefaultAzureCredentialOptions());
    var ctx = new TokenRequestContext(scopes: new[] { "https://management.azure.com/.default" });
    var token = await cred.GetTokenAsync(ctx, default(CancellationToken));
    return new TokenCredentials(token.Token);
}

async Task<bool> IsPaired(string resourceGroup, string namespaceName)
{
    var pairingStatus = await managementClient.DisasterRecoveryConfigs.GetAsync(
        resourceGroup,
        namespaceName,
        alias: Constants.ALIAS);
    return pairingStatus.Role != RoleDisasterRecovery.PrimaryNotReplicating;
}

async Task DeleteAllEntities(string resourceGroup, string namespaceName)
{
    Console.WriteLine("Clearing entities in secondary namespace");
    var topicsToDelete = await managementClient.Topics.ListByNamespaceAsync(resourceGroup, namespaceName);
    foreach (var topic in topicsToDelete)
    {
        // TODO: check for messages. Transfer before deleting topic
        await managementClient.Topics.DeleteAsync(resourceGroup, namespaceName, topic.Name);
    }
}

async Task<GeoNamespace> GetGeoNamespace()
{
    var ns = await adminClient.GetNamespacePropertiesAsync();
    if (!ns.Value.Alias.Equals(Constants.ALIAS, sc))
        throw new Exception("Alias in configuration does not match alias in this Service Bus namespace.");

    var current = ns.Value.Name;
    var isUsingPrimary = current.Equals(Constants.PRIMARY_NAMESPACE, sc);
    var partner = isUsingPrimary ? Constants.SECONDARY_NAMESPACE : Constants.PRIMARY_NAMESPACE;

    return new GeoNamespace()
    {
        CurrentNameSpace = current,
        CurrentNameSpaceId = namespaces?.SingleOrDefault(n => n.Name.Equals(current, sc))?.Id,
        CurrentResourceGroup = namespaces?.SingleOrDefault(n => n.Name.Equals(current, sc))?.Id.Split('/')[4],
        PartnerNameSpace = partner,
        PartnerNameSpaceId = namespaces?.SingleOrDefault(n => n.Name.Equals(partner, sc))?.Id,
        PartnerResourceGroup = namespaces?.SingleOrDefault(n => n.Name.Equals(partner, sc))?.Id.Split('/')[4],
        PartnerClient = new ServiceBusAdministrationClient(isUsingPrimary ?
                        Constants.SECONDARY_ROOT_MANAGE_CONNECTION : Constants.PRIMARY_ROOT_MANAGE_CONNECTION)
    };
}

class GeoNamespace
{
    public string? CurrentNameSpace { get; set; }
    public string? CurrentNameSpaceId { get; set; }
    public string? CurrentResourceGroup { get; set; }
    public string? PartnerNameSpace { get; set; }
    public string? PartnerNameSpaceId { get; set; }
    public string? PartnerResourceGroup { get; set; }
    public ServiceBusAdministrationClient? PartnerClient { get; set; }
}