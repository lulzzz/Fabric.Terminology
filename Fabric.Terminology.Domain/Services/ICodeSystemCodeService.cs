﻿namespace Fabric.Terminology.Domain.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using CallMeMaybe;

    using Fabric.Terminology.Domain.Models;

    public interface ICodeSystemCodeService
    {
        Maybe<ICodeSystemCode> GetCodeSystemCode(Guid codeGuid);

        IReadOnlyCollection<ICodeSystemCode> GetCodeSystemCodes(IEnumerable<Guid> codeGuids);

        Task<IBatchCodeSystemCodeResult> GetCodeSystemCodesBatchAsync(IEnumerable<string> codes);

        Task<IBatchCodeSystemCodeResult> GetCodeSystemCodesBatchAsync(IEnumerable<string> codes, IEnumerable<Guid> codeSystemGuids);

        Task<PagedCollection<ICodeSystemCode>> GetCodeSystemCodesAsync(
            IPagerSettings settings,
            bool includeRetired = false);

        Task<PagedCollection<ICodeSystemCode>> GetCodeSystemCodesAsync(
            IPagerSettings settings,
            Guid codeSystemGuid,
            bool includeRetired = false);

        Task<PagedCollection<ICodeSystemCode>> GetCodeSystemCodesAsync(
            IPagerSettings settings,
            IEnumerable<Guid> codeSystemGuids,
            bool includeRetired = false);

        Task<PagedCollection<ICodeSystemCode>> GetCodeSystemCodesAsync(
            string filterText,
            IPagerSettings pagerSettings,
            IEnumerable<Guid> codeSystemGuids,
            bool includeRetired = false);
    }
}