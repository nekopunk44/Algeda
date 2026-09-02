namespace Infrastructure.Identity
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string SuperAdmin = "SuperAdmin";
        public const string Realtor = "Realtor";
        public const string Client = "Client";

        public static readonly string[] All = [Admin, SuperAdmin, Realtor, Client];
    }
}
