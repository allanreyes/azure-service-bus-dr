namespace ServiceBusDR.Services
{
    public static class StringHelper
    {
        public static string ToFQNS(this string sbNamespace)
                => $"{sbNamespace}.servicebus.windows.net";
    }
}