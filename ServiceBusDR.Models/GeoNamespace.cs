namespace ServiceBusDR.Models
{
    public class GeoNamespace
    {
        // Current not always equal to primary namespace
        // After a failover, current is usually the secondary namespace

        public ServiceBusNamespace Current { get; set; }
        public ServiceBusNamespace Partner { get; set; }
    }
}