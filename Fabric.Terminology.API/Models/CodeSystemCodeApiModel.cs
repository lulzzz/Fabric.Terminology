﻿namespace Fabric.Terminology.API.Models
{
    using System;

    using Fabric.Terminology.Domain.Models;

    public class CodeSystemCodeApiModel : ICodeSystemCode
    {
        public Guid CodeGuid { get; set; }

        public string Code { get; set; }

        public string Name { get; set; }

        public Guid CodeSystemGuid { get; set; }

        public string CodeSystemName { get; set; }

        public bool Retired { get; set; }
    }
}