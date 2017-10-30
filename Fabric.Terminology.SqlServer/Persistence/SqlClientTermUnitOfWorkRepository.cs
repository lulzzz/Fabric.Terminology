namespace Fabric.Terminology.SqlServer.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using CallMeMaybe;

    using Fabric.Terminology.Domain;
    using Fabric.Terminology.Domain.Exceptions;
    using Fabric.Terminology.Domain.Models;
    using Fabric.Terminology.Domain.Persistence;
    using Fabric.Terminology.SqlServer.Caching;
    using Fabric.Terminology.SqlServer.Models.Dto;
    using Fabric.Terminology.SqlServer.Persistence.Factories;
    using Fabric.Terminology.SqlServer.Persistence.UnitOfWork;

    using Serilog;

    internal class SqlClientTermUnitOfWorkRepository : IClientTermUnitOfWorkRepository
    {
        private readonly ILogger logger;

        private readonly IClientTermUnitOfWork uow;

        private readonly IClientTermCacheManager cacheManager;

        public SqlClientTermUnitOfWorkRepository(
            ILogger logger,
            IClientTermUnitOfWork uow,
            IClientTermCacheManager cacheManager)
        {
            this.logger = logger;
            this.uow = uow;
            this.cacheManager = cacheManager;
        }

        public Maybe<IValueSet> GetValueSet(Guid valueSetGuid)
        {
            return this.uow.GetValueSetDescriptionDto(valueSetGuid)
                .Select(
                    dto =>
                        {
                            var factory = new ValueSetBackingItemFactory();
                            var item = factory.Build(dto);
                            ((ValueSetBackingItem)item).IsCustom = true;
                            var codes = this.GetCodes(item.ValueSetGuid);
                            var counts = this.GetCodeCounts(item.ValueSetGuid);

                            return new ValueSet(item, codes, counts) as IValueSet;
                        });
        }

        public Attempt<IValueSet> Add(IValueSet valueSet)
        {
            if (!EnsureIsNew(valueSet))
            {
                var invalid = new InvalidOperationException("Cannot save an existing value set as a new value set.");
                return Attempt<IValueSet>.Failed(invalid);
            }

            var valueSetGuid = valueSet.SetIdsForCustomInsert();

            this.uow.Commit(this.PerpareNewValueSetOperations(valueSet));

            // Get the updated ValueSet
            var added = this.GetValueSet(valueSetGuid);

            return added.Select(Attempt<IValueSet>.Successful)
                .Else(
                    () => Attempt<IValueSet>.Failed(
                        new ValueSetNotFoundException("Could not retrieved newly saved ValueSet")));
        }

        public Attempt<IValueSet> AddRemoveCodes(
            Guid valueSetGuid,
            IEnumerable<ICodeSystemCode> codesToAdd,
            IEnumerable<ICodeSystemCode> codesToRemove)
        {
            return this.uow.GetValueSetDescriptionDto(valueSetGuid)
                .Select(vsd =>
                {
                    if (vsd.StatusCD != ValueSetStatus.Draft.ToString())
                    {
                        return NotFoundAttempt();
                    }

                    throw new NotImplementedException();
                })
                .Else(NotFoundAttempt);

            Attempt<IValueSet> NotFoundAttempt() =>
                Attempt<IValueSet>.Failed(new ValueSetNotFoundException($"A value set in 'Draft' status with ValueSetGuid {valueSetGuid} could not be found."));
        }

        public void Delete(IValueSet valueSet)
        {
            using (var transaction = this.uow.Context.Database.BeginTransaction())
            {
                try
                {
                    this.uow.Context.BulkDelete(
                        new[] { typeof(ValueSetDescriptionBaseDto), typeof(ValueSetCodeDto), typeof(ValueSetCodeCountDto) },
                        new Dictionary<string, object>
                        {
                            { nameof(ValueSetDescriptionBaseDto.ValueSetGUID), valueSet.ValueSetGuid }
                        });
                    transaction.Commit();

                    this.cacheManager.Clear(valueSet.ValueSetGuid);
                }
                catch (Exception ex)
                {
                    var operationException = new ValueSetOperationException(
                        $"Failed to delete custom ValueSet with ID {valueSet.ValueSetGuid}",
                        ex);
                    this.logger.Error(operationException, "Failed to delete custom ValueSet");
                    throw operationException;
                }
            }
        }

        private static Operation BuildOperation(object value, OperationType operationType) =>
            new Operation { Value = value, OperationType = operationType };

        private static IEnumerable<Operation> BuildOperationBatch(IEnumerable<object> values, OperationType operationType)
            => values.Select(v => BuildOperation(v, operationType));

        private static bool EnsureIsNew(IValueSet valueSet) =>
            valueSet.IsCustom && valueSet.IsLatestVersion && valueSet.ValueSetGuid.Equals(Guid.Empty);

        private static bool EnsureIsDraft(IValueSet valueSet) => valueSet.StatusCode == ValueSetStatus.Draft;

        private Queue<Operation> PerpareNewValueSetOperations(IValueSet valueSet)
        {
            var operations =
                new List<Operation>
                    {
                        BuildOperation(new ValueSetDescriptionBaseDto(valueSet), OperationType.Create)
                    }
                    .Union(
                        BuildOperationBatch(
                            valueSet.ValueSetCodes.Select(code => new ValueSetCodeDto(code)),
                            OperationType.Create))
                    .Union(
                        BuildOperationBatch(
                            valueSet.CodeCounts.Select(count => new ValueSetCodeCountDto(count)),
                            OperationType.Create));

            return this.Enqueue(operations);
        }

        private void PrepareAddValueSet(
            Guid valueSetGuid,
            IEnumerable<Guid> valueSetGuids)
        {
            // TODO - add ability to grab all codes from an existing value set
            // TODO - and and insert into the current value set.
            throw new NotImplementedException();
        }

        // add ability to remove all codes that exist in current value set that also exist in 
        // value set passed.

        private Queue<Operation> PrepareAddRemoveCodes(
            Guid valueSetGuid,
            IReadOnlyCollection<ICodeSystemCode> codesToAdd,
            IReadOnlyCollection<ICodeSystemCode> codesToRemove)
        {
            var currentCodeDtos = this.uow.GetCodeDtos(valueSetGuid);

            // There could be duplicates between the codesToAdd and codesToRemove collection.
            // This is handled by simply doing the add operations first and then the remove operations.
            // In cases where a code is found in both lists, it will simply be removed from each list.
            // e.g. This is up to the UI to understand how the lists will be handled.
            // TODO document this!
            var addResult = this.PrepareAddCodesOperations(currentCodeDtos, codesToAdd);


            throw new NotImplementedException();
        }

        private BuildCodeOperataionResult PrepareAddCodesOperations(
            IReadOnlyCollection<ValueSetCodeDto> currentCodeDtos,
            IReadOnlyCollection<ICodeSystemCode> codesToAdd)
        {
            var addOps = this.BuildAddCodeOperationList(currentCodeDtos, codesToAdd);

            var allCodeDtos = currentCodeDtos.Union(
                                addOps.Where(ao => ao.OperationType == OperationType.Create)
                                    .Select(ao => ao.Value<ValueSetCodeDto>()));

            return new BuildCodeOperataionResult { CurrentCodeDtos = allCodeDtos.ToList(), Operations = addOps };
        }

        private BuildCodeOperataionResult PrepareRemoveCodesOperations(
            IReadOnlyCollection<ValueSetCodeDto> currentCodeDtos,
            IEnumerable<ICodeSystemCode> codes)
        {

            var removeOps = this.BuildRemoveCodesOperationList(currentCodeDtos, codes);
            var removedCodeGuids = removeOps.Select(ro => ro.Value<CodeSystemCodeDto>().CodeGUID);
            var resultDtos = currentCodeDtos.Where(oc => removedCodeGuids.All(rc => rc != oc.CodeGUID));

            return new BuildCodeOperataionResult { CurrentCodeDtos = resultDtos.ToList(), Operations = removeOps };
        }

        private Queue<Operation> Enqueue(IEnumerable<Operation> operations)
        {
            var queue = new Queue<Operation>();
            foreach (var o in operations)
            {
                queue.Enqueue(o);
            }

            return queue;
        }

        private IReadOnlyCollection<Operation> BuildAddCodeOperationList(
            IEnumerable<ValueSetCodeDto> originalCodeDtos,
            IEnumerable<ICodeSystemCode> codeSystemCodes)
        {
            var existingGuids = originalCodeDtos.Select(eg => eg.CodeGUID);
            return codeSystemCodes.Where(code => existingGuids.All(eg => eg != code.CodeGuid))
                .Select(
                    code => new Operation
                    {
                        Value = new ValueSetCodeDto(code),
                        OperationType = OperationType.Create
                    })
                .ToList();
        }

        private IReadOnlyCollection<Operation> BuildRemoveCodesOperationList(
            IEnumerable<ValueSetCodeDto> originalCodeDtos,
            IEnumerable<ICodeSystemCode> codeSystemCodes)
        {
            var destGuids = codeSystemCodes.Select(ds => ds.CodeGuid);
            return originalCodeDtos.Where(code => destGuids.All(dg => dg != code.CodeGUID))
                .Select(dto => new Operation
                {
                    Value = dto,
                    OperationType = OperationType.Delete
                }).ToList();
        }

        private IReadOnlyCollection<Operation> BuildCodeCountOperationList(
            Guid valueSetGuid,
            IEnumerable<ValueSetCodeDto> allCodeDtos)
        {
            var originalCounts = this.uow.GetCodeCountDtos(valueSetGuid);

            var newCodes = allCodeDtos as ValueSetCodeDto[] ?? allCodeDtos.ToArray();
            var allCodeSystems = newCodes.Select(c => c.CodeSystemGuid).Distinct();

            var newCounts = allCodeSystems.Select(
                            codeSystemGuid => new
                            {
                                codeSystemGuid,
                                valueSetDto = newCodes.First(dto => dto.CodeSystemGuid == codeSystemGuid)
                            })
                            .Select(o =>
                            new ValueSetCodeCountDto(o.valueSetDto.ValueSetGUID,
                                o.valueSetDto.CodeSystemGuid,
                                o.valueSetDto.CodeSystemNM,
                                newCodes.Count(c => c.CodeSystemGuid == o.codeSystemGuid)))
                                .ToList();

            var operations = newCounts.Select(nc =>
                Maybe.From(originalCounts.FirstOrDefault(ec => ec.CodeSystemGUID == nc.CodeSystemGUID))
                .Select(
                    dto =>
                        {
                            var op = new Operation();
                            if (dto.CodeSystemPerValueSetNBR != nc.CodeSystemPerValueSetNBR)
                            {
                                dto.CodeSystemPerValueSetNBR = nc.CodeSystemPerValueSetNBR;
                                op.OperationType = OperationType.Update;
                            }
                            else
                            {
                                op.OperationType = OperationType.None;
                            }

                            op.Value = dto;
                            return op;
                        })
                .Else(() => new Operation
                {
                    Value = nc
                })).ToList();

            // finally ensure that any existing counts that were not in the new counts are removed
            // e.g. all codes from a particular code system were removed
            var removers = originalCounts.Except(newCounts);

            operations.AddRange(removers.Select(r => new Operation
            {
               Value = r,
               OperationType = OperationType.Delete
            }));

            return operations;
        }

        private IReadOnlyCollection<IValueSetCode> GetCodes(Guid valueSetGuid)
        {
            var factory = new ValueSetCodeFactory();
            var codes = this.uow.GetCodeDtos(valueSetGuid);
            return codes.Select(factory.Build).ToList();
        }

        private IReadOnlyCollection<IValueSetCodeCount> GetCodeCounts(Guid valueSetGuid)
        {
            var factory = new ValueSetCodeCountFactory();
            var counts = this.uow.GetCodeCountDtos(valueSetGuid);
            return counts.Select(factory.Build).ToList();
        }

        private struct BuildCodeOperataionResult
        {
            public IReadOnlyCollection<ValueSetCodeDto> CurrentCodeDtos { get; set; }

            public IReadOnlyCollection<Operation> Operations { get; set; }
        }
    }
}