// ReSharper disable InconsistentNaming
namespace Fabric.Terminology.SqlServer.Models.Dto
{
    using System;

    using Fabric.Terminology.Domain.Models;

    internal sealed class ValueSetCodeDto
    {
        private static readonly EmptySamdBinding EmptyBinding = new EmptySamdBinding();

        public ValueSetCodeDto()
        {
        }

        public ValueSetCodeDto(IValueSetCode code)
            : this(code as ICodeSystemCode)
        {
            this.ValueSetGUID = code.ValueSetGuid;
        }

        public ValueSetCodeDto(ICodeSystemCode code)
        {
            this.CodeGUID = code.CodeGuid;
            this.CodeCD = code.Code;
            this.CodeDSC = code.Name;
            this.CodeSystemGuid = code.CodeSystemGuid;
            this.CodeSystemNM = code.CodeSystemName;
            this.LastModifiedDTS = DateTime.UtcNow;
            this.BindingID = EmptyBinding.BindingID;
            this.BindingNM = EmptyBinding.BindingNM;
            this.LastLoadDTS = EmptyBinding.LastLoadDts;
        }

        //// Order of the properties is important for SqlBulkCopy
        //// This should be fixed by passing a schema to the  DtoDataReader

        public int BindingID { get; set; }

        public string BindingNM { get; set; }

        public DateTime LastLoadDTS { get; set; }

        public Guid ValueSetGUID { get; set; }

        public Guid? CodeGUID { get; set; }

        public string CodeCD { get; set; }

        public string CodeDSC { get; set; }

        public Guid CodeSystemGuid { get; set; }

        public string CodeSystemNM { get; set; }

        public DateTime LastModifiedDTS { get; set; }
    }
}