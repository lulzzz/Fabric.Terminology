using AppFunc = System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;

namespace Fabric.Terminology.API.Bootstrapping.Middleware
{
    using System.Linq;
    using System.Threading.Tasks;

    using LibOwin;

    public class AuthorizationMiddleware
    {
        public static AppFunc Inject(AppFunc next, string[] requiredScopes, string[] allowedPaths = null)
        {
            return env =>
            {                
                var ctx = new OwinContext(env);
                if (ctx.Request.Method == "OPTIONS") return next(env);

                if (allowedPaths != null && allowedPaths.Contains(ctx.Request.Path.Value)) return next(env);

                var principal = ctx.Request.User;
                if (principal != null)
                {
                    if (requiredScopes.Any(requiredScope => principal.HasClaim("scope", requiredScope)))
                    {
                        return next(env);
                    }
                }
                
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", new[] { "*" });
                ctx.Response.Headers.Add("Access-Control-Allow-Headers", new[] { "Origin, X-Requested-With, Content-Type, Accept, Authorization" });
                ctx.Response.Headers.Add("Access-Control-Allow-Methods", new[] { "POST, GET, PUT, DELETE, PATCH" });
                ctx.Response.StatusCode = 403;

                return Task.CompletedTask;
            };
        }
    }
}
