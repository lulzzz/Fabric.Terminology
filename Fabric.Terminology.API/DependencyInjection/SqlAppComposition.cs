﻿using Fabric.Terminology.API.Configuration;
using Fabric.Terminology.Domain.DependencyInjection;
using Fabric.Terminology.SqlServer.Caching;
using Fabric.Terminology.SqlServer.Configuration;
using Fabric.Terminology.SqlServer.Persistence.DataContext;
using Nancy.TinyIoc;

namespace Fabric.Terminology.API.DependencyInjection
{
    public class SqlAppComposition : IContainerComposition<TinyIoCContainer>
    {
        public void Compose(TinyIoCContainer container)
        {           
            container.Register<TerminologySqlSettings>((c,s) => c.Resolve<IAppConfiguration>().TerminologySqlSettings);
            container.Register<SharedContextFactory>().AsSingleton();
        }
    }
}