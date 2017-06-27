﻿namespace Fabric.Terminology.API.Modules
{
    using System;
    using System.Net.Http.Headers;

    using Fabric.Terminology.API.Models;

    using FluentValidation;

    using Nancy;
    using Nancy.Responses.Negotiation;

    using Serilog;

    public abstract class TerminologyModule<T> : NancyModule
    {
        protected TerminologyModule(string path, ILogger logger, AbstractValidator<T> abstractValidator)
            : base(path)
        {
            this.Logger = logger;
            this.Validator = abstractValidator;
        }

        protected ILogger Logger { get; }

        protected AbstractValidator<T> Validator { get; }

        protected Negotiator CreateSuccessfulPostResponse(IIdentifiable model)
        {
            var uriBuilder = new UriBuilder(
                this.Request.Url.Scheme,
                this.Request.Url.HostName,
                this.Request.Url.Port ?? 80,
                $"{this.ModulePath}/{model.Identifier}");

            var selfLink = uriBuilder.ToString();

            return this.Negotiate
                .WithModel(model)
                .WithStatusCode(HttpStatusCode.Created)
                .WithHeader(Constants.HttpResponseHeaders.Location, selfLink);
        }

        protected Negotiator CreateFailureResponse(string message, HttpStatusCode statusCode)
        {
            var error = ErrorFactory.CreateError<T>(message, statusCode);
            return this.Negotiate.WithModel(error).WithStatusCode(statusCode);
        }
    }
}