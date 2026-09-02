namespace API.Auth
{
    public static class AuthorizationPolicies
    {
        public const string AdminOnly = "AdminOnly";
        public const string RealtorOrAdmin = "RealtorOrAdmin";
        public const string ClientOrAdmin = "ClientOrAdmin";
        public const string ClientOrRealtorOrAdmin = "ClientOrRealtorOrAdmin";
    }
}
