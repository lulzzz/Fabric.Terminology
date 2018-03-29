using BuildFunc = System.Action<System.Func<
                    System.Func<
                        System.Collections.Generic.IDictionary<string, object>,
                        System.Threading.Tasks.Task>,
                    System.Func<
                        System.Collections.Generic.IDictionary<string, object>,
                        System.Threading.Tasks.Task>
                   >>;

// ReSharper disable once CheckNamespace
namespace Fabric.Platform.Auth
{
    internal static class BuildFuncExtensions
    {
        public static BuildFunc UseAuthPlatform(this BuildFunc buildFunc, string[] requiredScopes, string[] allowedPaths = null)
        {
            buildFunc(next => AuthorizationMiddleware.Inject(next, requiredScopes, allowedPaths));
            return buildFunc;
        }
    }
}
