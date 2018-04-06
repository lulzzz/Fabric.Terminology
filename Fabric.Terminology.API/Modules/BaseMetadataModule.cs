namespace Fabric.Terminology.API.Modules
{
    using System.Collections.Generic;

    using Nancy.Swagger;
    using Nancy.Swagger.Modules;
    using Nancy.Swagger.Services;

    using Swagger.ObjectModel;
    using Swagger.ObjectModel.Builders;

    public abstract class BaseMetadataModule : SwaggerMetadataModule
    {
        protected BaseMetadataModule(ISwaggerModelCatalog modelCatalog, ISwaggerTagCatalog tagCatalog)
            : base(modelCatalog, tagCatalog)
        {
        }

        protected SecurityRequirementBuilder OAuth2ReadScopeBuilder => new SecurityRequirementBuilder()
            .SecurityScheme(SecuritySchemes.Oauth2)
            .SecurityScheme(new List<string> { Constants.Scopes.ReadScope, Constants.Scopes.TempScope });
    }
}