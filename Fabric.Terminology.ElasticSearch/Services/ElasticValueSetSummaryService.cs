﻿namespace Fabric.Terminology.ElasticSearch.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using CallMeMaybe;

    using Fabric.Terminology.Domain.Models;
    using Fabric.Terminology.Domain.Services;
    using Fabric.Terminology.ElasticSearch.Elastic;
    using Fabric.Terminology.ElasticSearch.Models;

    using Nest;

    using Serilog;

    public class ElasticValueSetSummaryService : IValueSetSummaryService
    {
        private readonly IValueSetIndexSearcher searcher;

        public ElasticValueSetSummaryService(IValueSetIndexSearcher searcher)
        {
            this.searcher = searcher;
        }

        public Maybe<IValueSetSummary> GetValueSetSummary(Guid valueSetGuid)
        {
            return this.searcher.Get(valueSetGuid).Select(Map);
        }

        public Maybe<IValueSetSummary> GetValueSetSummary(Guid valueSetGuid, IEnumerable<Guid> codeSystemGuids)
        {
            return this.GetValueSetSummary(valueSetGuid)
                .Select(
                    vss =>
                        {
                            return vss.CodeCounts.Any(cc => codeSystemGuids.Contains(cc.CodeSystemGuid)) ? vss : null;
                        });
        }

        public Task<IReadOnlyCollection<IValueSetSummary>> GetValueSetSummaries(IEnumerable<Guid> valueSetGuids)
        {
            return Task.FromResult((IReadOnlyCollection<IValueSetSummary>)this.searcher.GetMultiple(valueSetGuids).Select(Map));
        }

        public Task<IReadOnlyCollection<IValueSetSummary>> GetValueSetSummaries(IEnumerable<Guid> valueSetGuids, IEnumerable<Guid> codeSystemGuids)
        {
            return Task.FromResult((IReadOnlyCollection<IValueSetSummary>)
                this.searcher.GetMultiple(valueSetGuids)
                    .Where(vs => vs.CodeCounts.Any(cc => codeSystemGuids.Contains(cc.CodeSystemGuid)))
                    .Select(Map));
        }

        public Task<IReadOnlyCollection<IValueSetSummary>> GetValueSetVersions(string valueSetReferenceId)
        {
            var results = this.searcher.GetVersions(valueSetReferenceId).Select(Map).ToList();

            return Task.FromResult((IReadOnlyCollection<IValueSetSummary>)results);
        }

        public Task<PagedCollection<IValueSetSummary>> GetValueSetSummariesAsync(IPagerSettings settings, bool latestVersionsOnly = true)
        {
            return Task.FromResult(Map(this.searcher.GetPaged(settings, latestVersionsOnly)));
        }

        public Task<PagedCollection<IValueSetSummary>> GetValueSetSummariesAsync(IPagerSettings settings, IEnumerable<Guid> codeSystemGuids, bool latestVersionsOnly = true)
        {
            throw new NotImplementedException();
        }

        public Task<PagedCollection<IValueSetSummary>> GetValueSetSummariesAsync(string nameFilterText, IPagerSettings pagerSettings, bool latestVersionsOnly = true)
        {
            throw new NotImplementedException();
        }

        public Task<PagedCollection<IValueSetSummary>> GetValueSetSummariesAsync(
            string nameFilterText,
            IPagerSettings pagerSettings,
            IEnumerable<Guid> codeSystemGuids,
            bool latestVersionsOnly = true)
        {
            throw new NotImplementedException();
        }

        private static PagedCollection<IValueSetSummary> Map(PagedCollection<ValueSetIndexModel> ip)
        {
            return new PagedCollection<IValueSetSummary>
            {
                TotalPages = ip.TotalPages,
                TotalItems = ip.TotalItems,
                PagerSettings = ip.PagerSettings,
                Values = ip.Values.Select(Map).ToList()
            };
        }

        private static IValueSetSummary Map(ValueSetIndexModel model)
        {
            return new ValueSetSummary(model, model.CodeCounts);
        }
    }
}