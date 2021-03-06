﻿namespace Fabric.Terminology.IntegrationTests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Fabric.Terminology.API.Services;
    using Fabric.Terminology.Domain;
    using Fabric.Terminology.Domain.Models;
    using Fabric.Terminology.Domain.Services;
    using Fabric.Terminology.IntegrationTests.Fixtures;
    using Fabric.Terminology.TestsBase;
    using Fabric.Terminology.TestsBase.Mocks;

    using FluentAssertions;

    using Xunit;
    using Xunit.Abstractions;

    public class ClientTermValueSetServiceTests : OutputTestBase, IClassFixture<SqlServiceFixture>
    {
        private readonly IValueSetService valueSetService;

        private readonly IClientTermValueSetService clientTermValueSetService;

        private readonly IClientTermCustomizationService clientTermCustomizationService;

        public ClientTermValueSetServiceTests(SqlServiceFixture fixture, ITestOutputHelper output)
            : base(output)
        {
            this.clientTermValueSetService = fixture.ClientTermValueSetService;
            this.clientTermCustomizationService = fixture.ClientTermCustomizationService;
            this.valueSetService = fixture.ValueSetService;
        }

        [Theory]
        [InlineData("Code Count Check", 50)]
        public void CanCreateValueSet(string name, int codeCount)
        {
            // Arrange
            var apiModel = MockApiModelBuilder.ClientTermValueSetApiModel(name, codeCount);

            // Act
            //var attempt = this.clientTermValueSetService.Create(apiModel);
            var attempt = this.clientTermCustomizationService.CreateValueSet(apiModel);

            // Assert
            attempt.Success.Should().BeTrue();
            attempt.Result.Should().NotBeNull();

            var vs = attempt.Result;

            vs.ValueSetCodes.Should().NotBeEmpty();
            vs.CodeCounts.Should().NotBeEmpty();
            vs.CodeCounts.Sum(cc => cc.CodeCount).Should().Be(codeCount);
        }

        // TODO fix
        [Theory(Skip = "Codes can no longer be generated and must have a valid codeGuid")]
        [InlineData("Remove Test 1", 5, 2)]
        [InlineData("Remove Test 2", 4000, 24)]
        public void CanRemoveCodes(string name, int initialCodeCount, int removeCodeCount)
        {
            // Arrange
            var expectedCount = initialCodeCount - removeCodeCount;
            var apiModel = MockApiModelBuilder.ClientTermValueSetApiModel(name, initialCodeCount);
           // var setup = this.clientTermValueSetService.Create(apiModel);

            var setup = this.clientTermCustomizationService.CreateValueSet(apiModel);
            setup.Success.Should().BeTrue();
            setup.Result.Should().NotBeNull();
            var valueSet = setup.Result;
            this.clientTermValueSetService.SaveAsNew(valueSet);

            // Act
            //// try to remove the first codes.
            var removers = valueSet.ValueSetCodes.Batch(removeCodeCount).First();

            var result = this.Profiler.ExecuteTimed(() => this.clientTermValueSetService.AddRemoveCodes(
                                valueSet.ValueSetGuid,
                                new List<ICodeSystemCode>(),
                                removers))
                        .Result;

            // Assert
            result.ValueSetCodes.Count.Should().Be(expectedCount);

            var codeCodeSystemGuids = result.ValueSetCodes.Select(vs => vs.CodeSystemGuid).Distinct().ToList();
            var countCodeSystemsGuids = result.CodeCounts.Select(cc => cc.CodeSystemGuid).ToList();

            countCodeSystemsGuids.All(countCs => codeCodeSystemGuids.Contains(countCs)).Should().BeTrue();
            codeCodeSystemGuids.All(codeCs => countCodeSystemsGuids.Contains(codeCs)).Should().BeTrue();

            // cleanup
            this.clientTermValueSetService.Delete(result);
        }

        // TODO fix
        [Theory]
        [InlineData("Add Test 1", 5, 3)]
        [InlineData("Add Test 2", 4000, 24)]
        public void CanAddCodes(string name, int initialCodeCount, int addCodeCount)
        {
            // Arrange
            var expectedCount = initialCodeCount + addCodeCount;
            var apiModel = MockApiModelBuilder.ClientTermValueSetApiModel(name, initialCodeCount);

            var setup = this.clientTermCustomizationService.CreateValueSet(apiModel);

            setup.Success.Should().BeTrue();
            setup.Result.Should().NotBeNull();
            var valueSet = setup.Result;
            this.clientTermValueSetService.SaveAsNew(valueSet);

            var updateModel = MockApiModelBuilder.ClientTermValueSetApiModel(name, addCodeCount, true);

            // Act
            var result = this.Profiler.ExecuteTimed(
                    () => this.clientTermCustomizationService.UpdateValueSet(
                        valueSet.ValueSetGuid,
                        updateModel))
                .Result;

            // Assert
            result.ValueSetCodes.Count.Should().Be(expectedCount);

            var codeCodeSystemGuids = result.ValueSetCodes.Select(vsc => vsc.CodeSystemGuid).Distinct().ToList();
            var countCodeSystemsGuids = result.CodeCounts.Select(cc => cc.CodeSystemGuid).ToList();

            countCodeSystemsGuids.All(countCs => codeCodeSystemGuids.Contains(countCs)).Should().BeTrue();
            codeCodeSystemGuids.All(codeCs => countCodeSystemsGuids.Contains(codeCs)).Should().BeTrue();

            // cleanup
            this.clientTermValueSetService.Delete(result);
        }

        [Theory]
        [InlineData("Add Test 1", 5, 3, 2)]
        [InlineData("Add Test 1", 200, 100, 25)]
        public void CanAddAndRemoveCodes(string name, int initialCodeCount, int addCodeCount, int removeCodeCount)
        {
            // Arrange
            var expectedCount = initialCodeCount + addCodeCount - removeCodeCount; // no dups
            var apiModel = MockApiModelBuilder.ClientTermValueSetApiModel(name, initialCodeCount);

            //var setup = this.clientTermValueSetService.Create(apiModel);
            var setup = this.clientTermCustomizationService.CreateValueSet(apiModel);

            setup.Success.Should().BeTrue();
            setup.Result.Should().NotBeNull();
            var valueSet = setup.Result;
            this.clientTermValueSetService.SaveAsNew(valueSet);

            var removers = valueSet.ValueSetCodes.Batch(removeCodeCount).First();
            var codesToAdd = MockApiModelBuilder.CodeSetCodeApiModelCollection(addCodeCount);

            // Act
            var result = this.Profiler.ExecuteTimed(() => this.clientTermValueSetService.AddRemoveCodes(
                    valueSet.ValueSetGuid,
                    codesToAdd,
                    removers))
                .Result;

            // Assert
            result.ValueSetCodes.Count.Should().Be(expectedCount);

            var codeCodeSystemGuids = result.ValueSetCodes.Select(vsc => vsc.CodeSystemGuid).Distinct().ToList();
            var countCodeSystemsGuids = result.CodeCounts.Select(cc => cc.CodeSystemGuid).ToList();

            countCodeSystemsGuids.All(countCs => codeCodeSystemGuids.Contains(countCs)).Should().BeTrue();
            codeCodeSystemGuids.All(codeCs => countCodeSystemsGuids.Contains(codeCs)).Should().BeTrue();

            // cleanup
            this.clientTermValueSetService.Delete(result);
        }

        [Theory]
        [InlineData("Add VS 3", 5)]
        [InlineData("Add VS 2", 1000)]
        [InlineData("Add VS 4", 4000)]
        public void CanAddValueSet(string name, int codeCount)
        {
            // Arrange
            var apiModel = MockApiModelBuilder.ClientTermValueSetApiModel(name, codeCount);

            //var attempt = this.clientTermValueSetService.Create(apiModel);
            var attempt = this.clientTermCustomizationService.CreateValueSet(apiModel);

            attempt.Success.Should().BeTrue();
            attempt.Result.Should().NotBeNull();

            var vs = attempt.Result;

            // Act
            this.Profiler.ExecuteTimed(() => this.clientTermValueSetService.SaveAsNew(vs));

            // Assert
            vs.ValueSetGuid.Should().NotBe(Guid.Empty);
            vs.ValueSetCodes.Count.Should().Be(codeCount);
            vs.Name.Should().BeEquivalentTo(name);
            vs.IsLatestVersion.Should().BeTrue();
            vs.IsCustom.Should().BeTrue();
            vs.StatusCode.Should().Be(ValueSetStatus.Draft);
            vs.ValueSetCodes.All(x => x.CodeGuid != Guid.Empty).Should().BeTrue();

            // cleanup
            this.clientTermValueSetService.Delete(vs);
        }

        [Theory]
        [InlineData("35F6B1A6-A72B-48F5-B319-F6CCAF15734D", "Diabetes copy")] // bsql -> 2.4694085
        [InlineData("A2216AAC-8513-43D8-85C2-00057F92394B", "Hypotension copy")] // bsql ->  0.2887891
        [InlineData("A10BE32F-A086-41E2-B14F-9724E5D9DC29", "Trauma copy")] // bsql -> 48.5546188 seconds
        public void CanCopyValueSet(string valueSetReferenceId, string name)
        {
            // Arrange
            var valueSetGuid = Guid.Parse(valueSetReferenceId);
            var valueSet = this.valueSetService.GetValueSet(valueSetGuid).Single();
            var meta = MockApiModelBuilder.ClientTermValueSetApiModel(name, 1) as IValueSetMeta;

            // Act
            var attempt = this.Profiler.ExecuteTimed(() => this.clientTermValueSetService.Copy(valueSet, name, meta));

            // Assert
            attempt.Success.Should().BeTrue();
            attempt.Result.Should().NotBeNull();
            var copy = attempt.Result;
            copy.OriginGuid.Should().Be(valueSet.ValueSetGuid);
            copy.ValueSetCodes.Count.Should().Be(valueSet.ValueSetCodes.Count);
            copy.IsCustom.Should().BeTrue();
            copy.IsLatestVersion.Should().BeTrue();
            copy.StatusCode.Should().Be(ValueSetStatus.Draft);

            // cleanup
            this.clientTermValueSetService.Delete(copy);
        }
    }
}
