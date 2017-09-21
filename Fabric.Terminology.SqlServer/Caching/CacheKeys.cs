﻿namespace Fabric.Terminology.SqlServer.Caching
{
    using System;

    using Fabric.Terminology.Domain.Models;

    internal static class CacheKeys
    {
        public static string ValueSetBackingItemKey(Guid valueSetGuid)
        {
            return $"{typeof(IValueSetBackingItem)}-{valueSetGuid}";
        }

        public static string ValueSetCodeCountsKey(Guid valueSetGuid)
        {
            return $"{typeof(IValueSetCodeCount)}-{valueSetGuid}";
        }

        public static string ValueSetCodesKey(Guid valueSetGuid)
        {
            return $"{typeof(IValueSetCode)}-{valueSetGuid}";
        }
    }
}