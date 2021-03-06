﻿namespace Fabric.Terminology.SqlServer.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using CallMeMaybe;

    using Fabric.Terminology.Domain.Models;

    public interface ICodeSystemCodeRepository
    {
        Maybe<ICodeSystemCode> GetCodeSystemCode(Guid codeGuid);

        IReadOnlyCollection<ICodeSystemCode> GetCodeSystemCodes(IEnumerable<Guid> codeGuids);

        Task<IBatchCodeSystemCodeResult> GetCodeSystemCodesBatchAsync(
            IEnumerable<string> codes,
            IEnumerable<Guid> codeSystemGuids);

        Task<PagedCollection<ICodeSystemCode>> GetCodeSystemCodesAsync(
            string filterText,
            IPagerSettings pagerSettings,
            IEnumerable<Guid> codeSystemGuids,
            bool includeRetired);
    }
}