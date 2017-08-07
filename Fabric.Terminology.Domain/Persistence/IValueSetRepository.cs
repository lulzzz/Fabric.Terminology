﻿namespace Fabric.Terminology.Domain.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using CallMeMaybe;

    using Fabric.Terminology.Domain.Models;

    using JetBrains.Annotations;

    public interface IValueSetRepository
    {
        bool NameExists(string name);

        Maybe<IValueSet> GetValueSet(string valueSetUniqueId, IEnumerable<string> codeSystemCodes);

        IReadOnlyCollection<IValueSet> GetValueSets(
            IEnumerable<string> valueSetUniqueIds,
            IEnumerable<string> codeSystemCodes,
            bool includeAllValueSetCodes = false);

        Task<PagedCollection<IValueSet>> GetValueSetsAsync(
            IPagerSettings pagerSettings,
            IEnumerable<string> codeSystemCodes,
            bool includeAllValueSetCodes = false);

        Task<PagedCollection<IValueSet>> FindValueSetsAsync(
            string filterText,
            IPagerSettings pagerSettings,
            IEnumerable<string> codeSystemCodes,
            bool includeAllValueSetCodes = false);

        Attempt<IValueSet> Add(IValueSet valueSet);

        void Delete(IValueSet valueSet);
    }
}