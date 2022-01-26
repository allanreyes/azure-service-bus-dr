namespace SharedConfig
{
    public static class Constants
    {
        public const string AZURE_SUBSCRIPTION_ID = "";
        public const string ALIAS = "geo-canada";
        public const string PRIMARY_NAMESPACE = "primary-canada-east";
        public const string SECONDARY_NAMESPACE = "secondary-canada-central";

        public const string TOPIC_NAME = "order-created";
        public const string SUBSCRIPTION_NAME = "local-test";

        // Topic-level Shared access policy
        public const string GEO_SEND_CONNECTION = "";
        public const string GEO_LISTEN_CONNECTION = "";
        
        // Root-level Shared access policy
        public const string GEO_ROOT_MANAGE_CONNECTION = "";
        public const string PRIMARY_ROOT_MANAGE_CONNECTION = "";
        public const string SECONDARY_ROOT_MANAGE_CONNECTION = "";
    }
}