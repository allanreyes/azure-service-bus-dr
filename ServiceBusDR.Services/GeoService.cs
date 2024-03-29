﻿using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Azure.Management.ServiceBus;
using Microsoft.Azure.Management.ServiceBus.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using ServiceBusDR.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceBusDR.Services
{
    public class GeoService : IGeoService
    {
        private readonly string _alias;
        private readonly string _primary;
        private readonly string _secondary;
        private readonly ServiceBusAdministrationClient _adminClient;
        private readonly ServiceBusManagementClient _managementClient;
        private readonly List<SBNamespace> _namespaces;
        private StringComparison sc = StringComparison.InvariantCultureIgnoreCase;
        private readonly TokenCredential _cred;
        private readonly ILogger _logger;

        public GeoService(IConfiguration config, ILogger<GeoService> logger)
        {
            _logger = logger;
            _alias = config["Alias"];
            _primary = config["PrimaryNamespace"];
            _secondary = config["SecondaryNamespace"];

            // In Visual Studio, go to Tools > Options > Azure Service Authentication > Account Selection to make sure you're
            // using the correct Azure account, with required role assignment (Azure Service Bus Data Owner)
            _cred = new DefaultAzureCredential();
            _adminClient = new ServiceBusAdministrationClient(_alias.ToFQNS(), _cred);
            _managementClient = new ServiceBusManagementClient(GetTokenCredentials(_cred));
            _managementClient.SubscriptionId = config["SubscriptionId"];
            _namespaces = GetNamespaces(_managementClient);
        }

        public async Task<GeoNamespace> GetGeoNamespace()
        {
            var ns = await _adminClient.GetNamespacePropertiesAsync();
            if (!ns.Value.Alias.Equals(_alias, sc))
                throw new Exception("Alias in configuration does not match alias in this Service Bus namespace.");

            var current = ns.Value.Name;
            var currentNs = _namespaces?.SingleOrDefault(n => n.Name.Equals(current, sc));
            var isUsingPrimary = current.Equals(_primary, sc);
            var partner = isUsingPrimary ? _secondary : _primary;
            var partnerNs = _namespaces?.SingleOrDefault(n => n.Name.Equals(partner, sc));
            var rgIndex = Array.IndexOf(currentNs?.Id.Split('/'), currentNs?.Id.Split('/').FirstOrDefault(s => s.Trim().Equals("resourceGroups", StringComparison.InvariantCultureIgnoreCase)));

            var geoNs = new GeoNamespace()
            {
                Current = new ServiceBusNamespace(currentNs?.Id, current, currentNs?.Id.Split('/')[rgIndex + 1]),
                Partner = new ServiceBusNamespace(partnerNs?.Id, partner, partnerNs?.Id.Split('/')[rgIndex + 1])
            };

            return geoNs;
        }

        public async Task<ArmDisasterRecovery> GetPairingStatus(string resourceGroup, string namespaceName)
        {
            return await _managementClient.DisasterRecoveryConfigs.GetAsync(
                resourceGroup,
                namespaceName,
                alias: _alias);
        }

        private TokenCredentials GetTokenCredentials(TokenCredential cred)
        {
            var ctx = new TokenRequestContext(scopes: new[] { "https://management.azure.com/.default" });
            var token = cred.GetTokenAsync(ctx, default).Result;
            return new TokenCredentials(token.Token);
        }

        private static List<SBNamespace> GetNamespaces(ServiceBusManagementClient managementClient)
        {
            var namespaces = managementClient.Namespaces.List();
            var namespacesList = new List<SBNamespace>(namespaces);
            var nextPageLink = namespaces.NextPageLink;
            while (nextPageLink != null)
            {
                namespaces = managementClient.Namespaces.ListNext(nextPageLink);
                namespacesList.AddRange(namespaces);
                nextPageLink = namespaces.NextPageLink;
            }
            return namespacesList;
        }

        public async Task TransferMessages(GeoNamespace geo, string path)
        {
            var topic = path.Split('/')[0];
            var subscription = path.Split('/')[1];
            var senderClient = new ServiceBusClient(geo.Current.Name.ToFQNS(), credential: _cred);
            var receiverClient = new ServiceBusClient(geo.Partner.Name.ToFQNS(), credential: _cred);
            var sender = senderClient.CreateSender(topic);
            var receiver = receiverClient.CreateReceiver(topic, subscription, new ServiceBusReceiverOptions() { PrefetchCount = 300 });
                
            do
            {
                var messages = await receiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(10));
                if (!messages.Any()) break;

                _logger.LogInformation($"Copying {messages.Count} messages from source subscription '{subscription}' under topic '{topic}' ");
                await Send(messages, sender, receiver);
            }
            while (true);
        }

        public async Task<string[]> GetNonEmptyEntities(GeoNamespace geo)
        {
            var result = new List<string>();
            var partnerAdminClient = new ServiceBusAdministrationClient(geo.Partner.Name.ToFQNS(), credential: _cred);
            var topics = await _managementClient.Topics.ListByNamespaceAsync(geo.Partner.ResourceGroup, geo.Partner.Name);

            foreach (var topic in topics)
            {
                var subscriptions = await _managementClient.Subscriptions.ListByTopicAsync(geo.Partner.ResourceGroup, geo.Partner.Name, topic.Name);
                foreach (var subscription in subscriptions)
                {
                    var subProperties = await partnerAdminClient.GetSubscriptionRuntimePropertiesAsync(topic.Name, subscription.Name);
                    var activeMessageCount = subProperties.Value.ActiveMessageCount;
                    if (activeMessageCount > 0)
                    {
                        _logger.LogInformation($"Topic '{topic.Name}' Subscription '{subscription.Name}' has {activeMessageCount} active messages");
                        result.Add($"{topic.Name}/{subscription.Name}");
                    }
                }
            }
            return result.ToArray();
        }

        public async Task DeleteAllEntities(GeoNamespace geo)
        {
            var topicsToDelete = await _managementClient.Topics.ListByNamespaceAsync(geo.Partner.ResourceGroup, geo.Partner.Name);
            foreach (var topic in topicsToDelete)
            {
                _logger.LogInformation($"Deleting topic '{topic.Name}' from {geo.Partner.Name}");
                await _managementClient.Topics.DeleteAsync(geo.Partner.ResourceGroup, geo.Partner.Name, topic.Name);
            }
        }

        public async Task<ArmDisasterRecovery> InitiatePairing(GeoNamespace geo)
        {
            return await _managementClient.DisasterRecoveryConfigs.CreateOrUpdateAsync(
                   geo.Current.ResourceGroup,
                   namespaceName: geo.Current.Name,
                   alias: _alias,
                   new ArmDisasterRecovery() { PartnerNamespace = geo.Partner.Id });
        }

        public async Task ExecuteFailover(GeoNamespace geo)
        {
            await _managementClient.DisasterRecoveryConfigs
                .FailOverAsync(geo.Partner.ResourceGroup, geo.Partner.Name, alias: _alias);
        }

        private async Task Send(IReadOnlyList<ServiceBusReceivedMessage> messages, ServiceBusSender sender, ServiceBusReceiver receiver)
        {
            if (!messages.Any()) return;

            Console.WriteLine($"Sending {messages.Count} messages");

            var batch = await sender.CreateMessageBatchAsync();
            var index = 0;
            while (true)
            {
                var message = messages.ElementAtOrDefault(index);
                if (message == null) break;

                if (batch.TryAddMessage(new ServiceBusMessage(message)))
                {
                    index++;
                    var isLastMessage = index == messages.Count;

                    if (!isLastMessage)
                        continue;
                }

                await sender.SendMessagesAsync(batch); // Batch is full or end of messages
                Console.WriteLine($"Sent batch of {batch.Count} messages");
                batch = await sender.CreateMessageBatchAsync(); // Reset batch            
            }

            Console.WriteLine($"Completing {messages.Count} messages");
            foreach (var message in messages)
                try
                {
                    await receiver.CompleteMessageAsync(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                }
        }
    }

    public interface IGeoService
    {
        Task<GeoNamespace> GetGeoNamespace();
        Task<ArmDisasterRecovery> GetPairingStatus(string resourceGroup, string namespaceName);
        Task TransferMessages(GeoNamespace geo, string path);
        Task<string[]> GetNonEmptyEntities(GeoNamespace geo);
        Task DeleteAllEntities(GeoNamespace geo);
        Task<ArmDisasterRecovery> InitiatePairing(GeoNamespace geo);
        Task ExecuteFailover(GeoNamespace geo);
    }
}
