namespace Fabric.Terminology.API
{
    /// <summary>
    /// Represents constants used in swagger metadata modules.
    /// </summary>
    public partial class Constants
    {
        public static class Scopes
        {
            public static readonly string ReadScope = "fabric/terminology.read";

            public static readonly string TempScope = "HQCATALYST\\Population Builder";

            public static readonly string WriteScope = "fabric/terminology.write";
        }

        public static class IdentityScopes
        {
            public static readonly string ReadScope = "fabric/identity.read";
            public static readonly string SearchUsersScope = "fabric/identity.searchusers";
        }
    }
}
