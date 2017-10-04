﻿namespace Fabric.Terminology.API.Models
{
    using System;

    using Swagger.ObjectModel;

    public class CodeSetCodeApiModel
    {
        public Guid CodeGuid { get; internal set; }

        public string Code { get; internal set; }

        public string Name { get; internal set; }

        public Guid CodeSystemGuid { get; internal set; }

        public string CodeSystemName { get; internal set; }
    }
}