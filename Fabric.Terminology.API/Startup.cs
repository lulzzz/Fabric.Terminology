namespace Fabric.Terminology.API
{
    using System;

    using AutoMapper;

    using Fabric.Platform.Auth;
    using Fabric.Terminology.API.Configuration;
    using Fabric.Terminology.API.Logging;
    using Fabric.Terminology.API.Models;
    using Fabric.Terminology.Domain.Models;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Nancy.Owin;
    using Serilog;
    using Serilog.Core;

    using Swagger.ObjectModel;

    public class Startup
    {
        private readonly IAppConfiguration appConfig;

        private readonly string[] allowedPaths =
        {
            "/",
            "/swagger/index.html",
            "/api-docs/swagger.json",
            "/api-docs"
        };

        public Startup(IHostingEnvironment env)
        {
            this.appConfig = new TerminologyConfigurationProvider().GetAppConfiguration(env.ContentRootPath);

            var logger = LogFactory.CreateLogger(new LoggingLevelSwitch());
            Log.Logger = logger;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddWebEncoders();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IApplicationLifetime appLifetime)
        {
            loggerFactory.AddSerilog();

            Log.Logger.Information("Fabric.Terminology.API starting.");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            Log.Logger.Information("Initializing AutoMapper");

            Mapper.Initialize(cfg =>
                {
                    cfg.AddProfile<CodeSystemApiProfile>();
                    cfg.AddProfile<CodeSystemCodeApiProfile>();
                    cfg.AddProfile<ValueSetApiProfile>();
                });

            app.UseIdentityServerAuthentication(new IdentityServerAuthenticationOptions
            {
                Authority = this.appConfig.IdentityServerSettings.Authority,
                RequireHttpsMetadata = false,
                ApiName = this.appConfig.IdentityServerSettings.ClientId
            });

            app.UseStaticFiles()
                .UseOwin()
                .UseAuthPlatform(this.appConfig.IdentityServerSettings.Scopes, this.allowedPaths)
                .UseNancy(opt => opt.Bootstrapper = new Bootstrapper(this.appConfig, Log.Logger));

            Log.Logger.Information("Fabric.Terminology.API started!");
        }
    }
}
