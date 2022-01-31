namespace ServiceBusDR.Models
{
    public class ServiceBusNamespace
    {
        public ServiceBusNamespace(string id, string name, string resourceGroup)
        {
            Id = id;
            Name = name;
            ResourceGroup = resourceGroup;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string ResourceGroup { get; set; }
    }
}
