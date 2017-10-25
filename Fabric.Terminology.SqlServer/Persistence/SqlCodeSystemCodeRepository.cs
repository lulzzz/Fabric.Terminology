namespace Fabric.Terminology.SqlServer.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using CallMeMaybe;

    using Fabric.Terminology.Domain;
    using Fabric.Terminology.Domain.Models;
    using Fabric.Terminology.Domain.Persistence;
    using Fabric.Terminology.SqlServer.Caching;
    using Fabric.Terminology.SqlServer.Models.Dto;
    using Fabric.Terminology.SqlServer.Persistence.DataContext;
    using Fabric.Terminology.SqlServer.Persistence.Factories;

    using Microsoft.EntityFrameworkCore;

    using Serilog;

    internal class SqlCodeSystemCodeRepository : ICodeSystemCodeRepository
    {
        private readonly SharedContext sharedContext;

        private readonly ILogger logger;

        private readonly IPagingStrategyFactory pagingStrategyFactory;

        private readonly ICodeSystemCodeCachingManager codeSystemCodeCachingManager;

        public SqlCodeSystemCodeRepository(
            SharedContext sharedContext,
            ILogger logger,
            ICodeSystemCodeCachingManager codeSystemCodeCachingManager,
            IPagingStrategyFactory pagingStrategyFactory)
        {
            this.sharedContext = sharedContext;
            this.logger = logger;
            this.pagingStrategyFactory = pagingStrategyFactory;
            this.codeSystemCodeCachingManager = codeSystemCodeCachingManager;
        }

        public Maybe<ICodeSystemCode> GetCodeSystemCode(Guid codeGuid)
        {
            try
            {
                return this.codeSystemCodeCachingManager.GetOrSet(codeGuid, this.QueryCodeSystemCode);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"Failed to get CodeSystemCode with Guid: {codeGuid}");
                throw;
            }
        }

        public IReadOnlyCollection<ICodeSystemCode> GetCodeSystemCodes(IEnumerable<Guid> codeGuids)
        {
            try
            {
                return this.codeSystemCodeCachingManager.GetMultipleOrQuery(
                    this.QueryCodeSystemCodeList,
                    true,
                    codeGuids.ToArray());
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, $"Failed to get a list CodeSystemCodes");
                throw;
            }
        }

        public async Task<IBatchCodeSystemCodeResult> GetCodeSystemCodesBatchAsync(
            IEnumerable<string> codes,
            IEnumerable<Guid> codeSystemGuids)
        {
            var codesHash = codes.ToHashSet();

            var systemGuids = codeSystemGuids as Guid[] ?? codeSystemGuids.ToArray();

           var results = (await this.GetCodesBatchByJoin(codesHash, systemGuids)).ToList();

            //var results = (await Task.WhenAll(
            //                    codesHash.Batch(1500)
            //                    .Select(
            //                        batch =>
            //                            this.GetCodesByBatch(batch.ToList(), systemGuids)))
            //              )
            //              .SelectMany(codeList => codeList).ToList();

            return new BatchCodeSystemCodeResult
            {
                Matches = results,
                NotFound = codesHash.Where(c => !results.Exists(f => f.Code == c)).ToList()
            };
        }

        public Task<PagedCollection<ICodeSystemCode>> GetCodeSystemCodesAsync(
            string filterText,
            IPagerSettings pagerSettings,
            IEnumerable<Guid> codeSystemGuids,
            bool includeRetired)
        {
            var dtos = this.GetBaseQuery(includeRetired);
            var systemGuids = codeSystemGuids as Guid[] ?? codeSystemGuids.ToArray();
            if (systemGuids.Any())
            {
                dtos = dtos.Where(dto => systemGuids.Contains(dto.CodeSystemGUID));
            }

            if (!filterText.IsNullOrWhiteSpace())
            {
                dtos = dtos.Where(dto => dto.CodeDSC.Contains(filterText) || dto.CodeCD.StartsWith(filterText));
            }

            return this.CreatePagedCollectionAsync(dtos, pagerSettings);
        }

        private async Task<IReadOnlyCollection<ICodeSystemCode>> GetCodesByBatch(
            IReadOnlyCollection<string> codes,
            IReadOnlyCollection<Guid> codeSystemGuids)
        {
            var dtos = this.GetBaseQuery(true).Where(dto => codes.Contains(dto.CodeCD));
            if (codeSystemGuids.Any())
            {
                dtos = dtos.Where(dto => codeSystemGuids.Contains(dto.CodeSystemGUID));
            }

            var factory = new CodeSystemCodeFactory();
            var results = await dtos.ToListAsync();

            return results.Select(factory.Build).ToList();
        }

        private async Task<IReadOnlyCollection<ICodeSystemCode>> GetCodesBatchByJoin(HashSet<string> codes, IReadOnlyCollection<Guid> codeSystemGuids)
        {
            this.sharedContext.ChangeTracker.AutoDetectChangesEnabled = false;

            var batchId = GuidComb.GenerateComb();
            foreach (var code in codes)
            {
                this.sharedContext.BatchCodeQuery.Add(new BatchCodeQueryDto { CodeCD = code, BatchID = batchId });
            }

            this.sharedContext.ChangeTracker.DetectChanges();
            this.sharedContext.SaveChanges();
            this.sharedContext.ChangeTracker.AutoDetectChangesEnabled = true;

            // TODO review not sure this needs to be reset
            this.sharedContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var dtos = this.GetBaseQuery(true)
                .Join(
                    this.sharedContext.BatchCodeQuery.Where(b => b.BatchID == batchId),
                    left => left.CodeCD,
                    right => right.CodeCD,
                    (left, right) => left);

            if (codeSystemGuids.Any())
            {
                dtos = dtos.Where(dto => codeSystemGuids.Contains(dto.CodeSystemGUID));
            }

            var factory = new CodeSystemCodeFactory();
            var results = await dtos.ToListAsync();

            this.sharedContext.Database.ExecuteSqlCommand(
                "DELETE FROM [ClientTerm].[ApiBatchQuery] WHERE BatchID = {0}",
                batchId);

            return results.Select(factory.Build).ToList();
        }

        private IQueryable<CodeSystemCodeDto> GetBaseQuery(bool includeRetired)
        {
            return includeRetired
                       ? this.sharedContext.CodeSystemCodes.Include(codeDto => codeDto.CodeSystem)
                       : this.sharedContext.CodeSystemCodes.Include(codeDto => codeDto.CodeSystem)
                           .Where(dto => dto.RetiredFLG == "N");
        }

        private ICodeSystemCode QueryCodeSystemCode(Guid codeGuid)
        {
            var factory = new CodeSystemCodeFactory();
            var dto = this.GetBaseQuery(true).SingleOrDefault(d => d.CodeGUID == codeGuid);
            return dto != null ? factory.Build(dto) : null;
        }

        private IReadOnlyCollection<ICodeSystemCode> QueryCodeSystemCodeList(bool includeRetired, Guid[] codeGuids)
        {
            if (!codeGuids.Any())
            {
                return new List<ICodeSystemCode>();
            }

            var factory = new CodeSystemCodeFactory();
            var dtos = this.GetBaseQuery(includeRetired).Where(dto => codeGuids.Contains(dto.CodeGUID));
            if (!includeRetired)
            {
                dtos = dtos.Where(dto => dto.RetiredFLG == "N");
            }

            return dtos.ToList().Select(factory.Build).ToList();
        }

        private async Task<PagedCollection<ICodeSystemCode>> CreatePagedCollectionAsync(
            IQueryable<CodeSystemCodeDto> source,
            IPagerSettings pagerSettings)
        {
            var defaultItemsPerPage = this.sharedContext.Settings.DefaultItemsPerPage;
            var pagingStrategy = this.pagingStrategyFactory.GetPagingStrategy<ICodeSystemCode>(defaultItemsPerPage);

            pagingStrategy.EnsurePagerSettings(pagerSettings);

            var count = await source.CountAsync();
            var items = await source.OrderBy(dto => dto.CodeDSC)
                            .Skip((pagerSettings.CurrentPage - 1) * pagerSettings.ItemsPerPage)
                            .Take(pagerSettings.ItemsPerPage)
                            .ToListAsync();

            var factory = new CodeSystemCodeFactory();

            return pagingStrategy.CreatePagedCollection(
                items.Select(i => this.codeSystemCodeCachingManager.GetOrSet(i.CodeGUID, () => factory.Build(i))
                ).Values(),
                count,
                pagerSettings);
        }
    }
}
