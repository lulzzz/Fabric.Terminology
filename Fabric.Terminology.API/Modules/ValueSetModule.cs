﻿namespace Fabric.Terminology.API.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using AutoMapper;

    using Fabric.Terminology.API.Configuration;
    using Fabric.Terminology.API.Models;
    using Fabric.Terminology.API.Services;
    using Fabric.Terminology.API.Validators;
    using Fabric.Terminology.Domain;
    using Fabric.Terminology.Domain.Exceptions;
    using Fabric.Terminology.Domain.Models;
    using Fabric.Terminology.Domain.Services;

    using Nancy;
    using Nancy.ModelBinding;

    using Serilog;

    public sealed class ValueSetModule : TerminologyModule<ValueSetApiModel>
    {
        private readonly IClientTermValueSetService clientTermValueSetService;

        private readonly IClientTermCustomizationService clientTermCustomizationService;

        private readonly IValueSetComparisonService valueSetComparisonService;

        private readonly IValueSetService valueSetService;

        private readonly IValueSetSummaryService valueSetSummaryService;

        private readonly ValueSetValidator valueSetValidator;

        public ValueSetModule(
            IValueSetService valueSetService,
            IValueSetSummaryService valueSetSummaryService,
            IClientTermValueSetService clientTermValueSetService,
            IClientTermCustomizationService clientTermCustomizationService,
            IValueSetComparisonService valueSetComparisonService,
            IAppConfiguration config,
            ILogger logger,
            ValueSetValidator valueSetValidator)
            : base($"/{TerminologyVersion.Route}/valuesets", config, logger)
        {
            this.valueSetService = valueSetService;
            this.valueSetSummaryService = valueSetSummaryService;
            this.clientTermValueSetService = clientTermValueSetService;
            this.clientTermCustomizationService = clientTermCustomizationService;
            this.valueSetComparisonService = valueSetComparisonService;
            this.valueSetValidator = valueSetValidator;

            this.Get("/", async _ => await this.GetValueSetPage(), null, "GetPaged");

            this.Get("/{valueSetGuid}", parameters => this.GetValueSet(parameters.valueSetGuid), null, "GetValueSet");

            this.Get(
                "/versions/{referenceId}",
                parameters => this.GetValueSetVersions(parameters.referenceId),
                null,
                "GetValueSetVersions");

            this.Post("/multiple/", _ => this.GetMultipleValueSets(), null, "GetMultipleValueSets");

            this.Post("/search/", async _ => await this.Search(), null, "Search");

            this.Post("/copy/", _ => this.CopyValueSet(), null, "CopyValueSet");

            this.Post("/compare/", async _ => await this.CompareValueSets(), null, "CompareValueSets");

            this.Post("/", _ => this.AddValueSet(), null, "AddValueSet");

            this.Patch("/{valueSetGuid}", parameters => this.PatchValueSet(parameters.valueSetGuid), null, "PatchValueSet");

            this.Put(
                "/{valueSetGuid}/statuscode/{statusCode}",
                parameters => this.ChangeStatus(parameters.valueSetGuid, parameters.statusCode),
                null,
                "ChangeValueSetStatus");

            this.Delete("/{valueSetGuid}", parameters => this.DeleteValueSet(parameters.valueSetGuid), null, "DeleteValueSet");
        }

        private static ValueSetApiModel MapToValueSetApiModel(IValueSet vs, IReadOnlyCollection<Guid> codeSystemGuids)
        {
            return vs.ToValueSetApiModel(codeSystemGuids);
        }

        private static ValueSetItemApiModel MapToValueSetItemApiModel(
            IValueSetSummary vss,
            IReadOnlyCollection<Guid> codeSystemGuids)
        {
            return vss.ToValueSetItemApiModel(codeSystemGuids);
        }

        private static async Task<T> Execute<T>(Func<Task<T>> query)
        {
            return await query.Invoke();
        }

        private static MultipleValueSetsQuery EnsureQueryModel(MultipleValueSetsQuery model)
        {
            if (model.ValueSetGuids == null)
            {
                model.ValueSetGuids = new Guid[] { };
            }

            if (model.CodeSystemGuids == null)
            {
                model.CodeSystemGuids = new Guid[] { };
            }

            return model;
        }

        private object GetValueSet(string valueSetGuidString)
        {
            return this.ParseValueSetGuidAndExecute(valueSetGuidString, GetValueSetByGuid);

            object GetValueSetByGuid(Guid valueSetGuid)
            {
                var codeSystemGuids = this.GetCodeSystems();
                var summary = this.GetSummarySetting();

                try
                {
                    var model = summary
                                    ? this.valueSetSummaryService.GetValueSetSummary(valueSetGuid, codeSystemGuids)
                                        .Select(vs => (object)vs.ToValueSetItemApiModel(codeSystemGuids))
                                    : this.valueSetService.GetValueSet(valueSetGuid, codeSystemGuids)
                                        .Select(vs => (object)vs.ToValueSetApiModel(codeSystemGuids));

                    return model.Else(
                        () => this.CreateFailureResponse(
                            "ValueSet with matching ID was not found",
                            HttpStatusCode.NotFound));
                }
                catch (ValueSetNotFoundException ex)
                {
                    this.Logger.Error(ex, ex.Message, valueSetGuid.ToString());
                    return this.CreateFailureResponse(
                        $"The ValueSet with id: {valueSetGuid} was not found.",
                        HttpStatusCode.InternalServerError);
                }
            }
        }

        private object GetMultipleValueSets()
        {
            try
            {
                var model = EnsureQueryModel(this.Bind<MultipleValueSetsQuery>(new BindingConfig { BodyOnly = true }));

                if (!model.ValueSetGuids.Any())
                {
                    return this.CreateFailureResponse(
                        "An array of value set ids is required.",
                        HttpStatusCode.BadRequest);
                }

                return model.Summary
                           ? Execute(
                                   () => this.valueSetSummaryService.GetValueSetSummariesListAsync(
                                       model.ValueSetGuids,
                                       model.CodeSystemGuids))
                               .Result.Select(vss => vss.ToValueSetItemApiModel(model.CodeSystemGuids))
                               .ToList()
                           : (IReadOnlyCollection<object>)Execute(
                                   () => this.valueSetService.GetValueSetsListAsync(
                                       model.ValueSetGuids,
                                       model.CodeSystemGuids))
                               .Result.Select(vs => vs.ToValueSetApiModel(model.CodeSystemGuids))
                               .ToList();
            }
            catch (Exception ex)
            {
                this.Logger.Error(ex, ex.Message);
                return this.CreateFailureResponse("Failed to retrieve value sets", HttpStatusCode.InternalServerError);
            }
        }

        private object GetValueSetVersions(string valueSetReferenceId)
        {
            try
            {
                var codeSystemGuids = this.GetCodeSystems();
                var summary = this.GetSummarySetting();

                var versions = summary
                                   ? Execute(
                                           () => this.valueSetSummaryService.GetValueSetVersionsAsync(
                                               valueSetReferenceId))
                                       .Result.Select(vss => vss.ToValueSetItemApiModel(codeSystemGuids))
                                   : Execute(
                                           () => this.valueSetSummaryService.GetValueSetVersionsAsync(
                                               valueSetReferenceId))
                                       .Result.Select(vs => vs.ToValueSetItemApiModel(codeSystemGuids))
                                       .ToList();

                if (!versions.Any())
                {
                    return this.CreateFailureResponse(
                        "ValueSet with matching ValueSetReferenceID was not found",
                        HttpStatusCode.NotFound);
                }

                return versions;
            }
            catch (Exception ex)
            {
                this.Logger.Error(ex, ex.Message);
                return this.CreateFailureResponse("Failed to retrieve value sets", HttpStatusCode.InternalServerError);
            }
        }

        private async Task<object> GetValueSetPage()
        {
            try
            {
                var summary = this.GetSummarySetting();
                var status = this.GetValueSetStatusCode();
                var pagerSettings = this.GetPagerSettings();
                var codeSystemGuids = this.GetCodeSystems();

                return summary
                           ? (await this.valueSetSummaryService.GetValueSetSummariesAsync(
                                  pagerSettings,
                                  codeSystemGuids, 
                                  status)).ToValueSetApiModelPage(codeSystemGuids, MapToValueSetItemApiModel)
                           : (await this.valueSetService.GetValueSetsAsync(pagerSettings, codeSystemGuids, status))
                           .ToValueSetApiModelPage(codeSystemGuids, MapToValueSetApiModel);
            }
            catch (Exception ex)
            {
                this.Logger.Error(ex, ex.Message);
                return this.CreateFailureResponse(
                    ex.Message,
                    HttpStatusCode.InternalServerError);
            }
        }

        private async Task<object> Search()
        {
            try
            {
                var model = this.EnsureQueryModel(this.Bind<ValueSetFindByTermQuery>(new BindingConfig { BodyOnly = true }));

                var codeSystemGuids = model.CodeSystemGuids.ToList();

                return model.Summary
                           ? (await this.valueSetSummaryService.GetValueSetSummariesAsync(
                                  model.Term,
                                  model.PagerSettings,
                                  codeSystemGuids,
                                  model.StatusCodes))
                            .ToValueSetApiModelPage(codeSystemGuids, MapToValueSetItemApiModel)

                           : (await this.valueSetService.GetValueSetsAsync(
                                  model.Term,
                                  model.PagerSettings,
                                  codeSystemGuids,
                                  model.StatusCodes))
                            .ToValueSetApiModelPage(codeSystemGuids, MapToValueSetApiModel);
            }
            catch (Exception ex)
            {
                this.Logger.Error(ex, ex.Message);
                return this.CreateFailureResponse(ex.Message, HttpStatusCode.InternalServerError);
            }
        }

        private object AddValueSet()
        {
            try
            {
                var model = this.Bind<ClientTermValueSetApiModel>();

                var attempt = this.clientTermCustomizationService.CreateValueSet(model);
                if (!attempt.Success || attempt.Result == null)
                {
                    throw attempt.Exception ?? new ArgumentException("Failed to add value set.");
                }

                var valueSet = attempt.Result;
                var validationResult = this.valueSetValidator.Validate(valueSet);
                if (validationResult.IsValid)
                {
                    this.clientTermValueSetService.SaveAsNew(valueSet);
                    return this.CreateSuccessfulPostResponse(Mapper.Map<IValueSet, ValueSetApiModel>(valueSet));
                }

                throw new InvalidOperationException(
                    "ValueSet validation failed: "
                    + string.Join(Environment.NewLine, validationResult.Errors.Select(vr => vr.ErrorMessage)));
            }
            catch (Exception ex)
            {
                this.Logger.Error(ex, ex.Message);
                return this.CreateFailureResponse("Failed to create a value set", HttpStatusCode.InternalServerError);
            }
        }

        private object PatchValueSet(string valueSetGuidString)
        {
            return this.ParseValueSetGuidAndExecute(valueSetGuidString, PatchValueSetWithValueSetGuid);

            object PatchValueSetWithValueSetGuid(Guid valueSetGuid)
            {
                try
                {
                    var model = this.Bind<ClientTermValueSetApiModel>();

                    var validated = ValueSetPatchHelper.ValidatePatchModel(valueSetGuid, model);
                    if (!validated.Success)
                    {
                        return this.CreateFailureResponse(
                            validated.Exception == null ? "Validation failed" : validated.Exception.Message,
                            HttpStatusCode.BadRequest);
                    }

                    return this.valueSetService.GetValueSet(valueSetGuid)
                        .Select(vs =>
                            {
                                var attempt = this.clientTermCustomizationService.UpdateValueSet(valueSetGuid, model);
                                if (!attempt.Success || attempt.Result == null)
                                {
                                    throw attempt.Exception ?? new InvalidOperationException("Failed to patch value set");
                                }

                                return this.CreateSuccessfulPostResponse(Mapper.Map<IValueSet, ValueSetApiModel>(attempt.Result));
                            })
                        .Else(() =>
                            this.CreateFailureResponse(
                                $"Could not find ValueSet with id: {valueSetGuid}",
                                HttpStatusCode.NotFound));
                }
                catch (Exception ex)
                {
                    this.Logger.Error(ex, ex.Message);
                    return this.CreateFailureResponse("Failed to update a value set", HttpStatusCode.InternalServerError);
                }
            }
        }

        private object DeleteValueSet(string valueSetGuidString)
        {
            return this.ParseValueSetGuidAndExecute(valueSetGuidString, DeleteValueSetWithValueSetGuid);

            object DeleteValueSetWithValueSetGuid(Guid valueSetGuid)
            {
                return this.valueSetService.GetValueSet(valueSetGuid)
                    .Select(vs =>
                        {
                            if (vs.StatusCode != ValueSetStatus.Draft)
                            {
                                return this.CreateFailureResponse(
                                    "Invalid ValueSet status.  Delete operation is only permitted for 'Draft' status ValueSets",
                                    HttpStatusCode.BadRequest);
                            }

                            try
                            {
                                this.clientTermValueSetService.Delete(vs);
                                return this.Negotiate.WithStatusCode(HttpStatusCode.OK);
                            }
                            catch (Exception ex)
                            {
                                this.Logger.Error(ex, ex.Message, valueSetGuid);
                                return this.CreateFailureResponse(
                                    $"Failed to delete ValueSet with id: {valueSetGuid}",
                                    HttpStatusCode.InternalServerError);
                            }
                        })
                    .Else(() =>
                        this.CreateFailureResponse(
                            $"Could not find ValueSet with id: {valueSetGuid}",
                            HttpStatusCode.NotFound));
            }
        }

        private object CopyValueSet()
        {
            try
            {
                var model = this.Bind<ValueSetCopyApiModel>();

                var copy = this.valueSetService.GetValueSet(model.OriginGuid)
                            .Select(vs =>
                            {
                                var attempt = this.clientTermValueSetService.Copy(vs, model.Name, model);

                                return attempt.Success
                                           ? this.CreateSuccessfulPostResponse(attempt.Result.ToValueSetApiModel())
                                           : CreateAttemptFailureResponse(attempt);
                            });

                return copy.Else(
                    () => this.CreateFailureResponse(
                        $"ValueSet with matching ID: {model.OriginGuid} was not found",
                        HttpStatusCode.NotFound));
            }
            catch (Exception ex)
            {
                this.Logger.Error(ex, ex.Message);
                return this.CreateFailureResponse("Failed to copy a value set", HttpStatusCode.InternalServerError);
            }

            object CreateAttemptFailureResponse(Attempt<IValueSet> attempt) =>
                attempt.Exception != null
                    ? this.CreateFailureResponse(attempt.Exception.Message, HttpStatusCode.InternalServerError)
                    : this.CreateFailureResponse("Failed to copy ValueSet", HttpStatusCode.InternalServerError);
        }

        private async Task<object> CompareValueSets()
        {
            try
            {
                var model = this.Bind<CompareValueSetsQuery>();

                var comparison = await this.valueSetComparisonService.CompareValueSetCodes(
                                     model.ValueSetGuids,
                                     model.CodeSystemGuids);

                return comparison.ToValueSetComparisonResultApiModel(model.CodeSystemGuids);
            }
            catch (Exception ex)
            {
                this.Logger.Error(ex, ex.Message);
                return this.CreateFailureResponse("Failed to compare value sets", HttpStatusCode.InternalServerError);
            }
        }

        private object ChangeStatus(Guid valueSetGuid, string statusCode)
        {
            if (Enum.TryParse(statusCode, true, out ValueSetStatus status))
            {
                var attempt = this.clientTermValueSetService.ChangeStatus(valueSetGuid, status);
                try
                {
                    if (attempt.Success)
                    {
                        return this.CreateSuccessfulPostResponse(attempt.Result.ToValueSetApiModel());
                    }

                    if (attempt.Exception != null)
                    {
                        var msg = attempt.Exception.Message;
                        return this.CreateFailureResponse(msg, HttpStatusCode.BadRequest);
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.Error(ex, ex.Message, valueSetGuid.ToString(), status.ToString());
                    return this.CreateFailureResponse(
                        $"Failed to change the status code for the value set with id: {valueSetGuid}",
                        HttpStatusCode.InternalServerError);
                }
            }

            return this.CreateFailureResponse($"Failed to cast '{statusCode}' to a valid ValueSetStatus", HttpStatusCode.InternalServerError);
        }

        private bool GetSummarySetting()
        {
            var val = (string)this.Request.Query["$summary"];
            bool.TryParse(val, out var ret);
            return val.IsNullOrWhiteSpace() || ret;
        }

        private IEnumerable<ValueSetStatus> GetValueSetStatusCode()
        {
            var statuses = this.CreateParameterArray((string)this.Request.Query["$status"]);
            foreach (var status in statuses)
            {
                if (!Enum.TryParse(status, true, out ValueSetStatus statusCode))
                {
                    statusCode = ValueSetStatus.Active;
                }

                yield return statusCode;
            }
        }

        private ValueSetFindByTermQuery EnsureQueryModel(ValueSetFindByTermQuery model)
        {
            if (model.PagerSettings == null)
            {
                model.PagerSettings = new PagerSettings
                {
                    CurrentPage = 1,
                    ItemsPerPage = this.Config.TerminologySqlSettings.DefaultItemsPerPage
                };
            }

            if (model.CodeSystemGuids == null)
            {
                model.CodeSystemGuids = new Guid[] { };
            }

            if (model.Term == null)
            {
                model.Term = string.Empty;
            }

            return model;
        }
    }
}