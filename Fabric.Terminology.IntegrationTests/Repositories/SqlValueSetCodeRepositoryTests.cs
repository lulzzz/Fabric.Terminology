﻿namespace Fabric.Terminology.IntegrationTests.Repositories
{
    using System.Linq;
    using Fabric.Terminology.Domain.Persistence;
    using Fabric.Terminology.IntegrationTests.Fixtures;
    using Fabric.Terminology.TestsBase;
    using Xunit;
    using Xunit.Abstractions;

    public class SqlValueSetCodeRepositoryTests : OutputTestBase, IClassFixture<ValueSetCodeRepositoryFixture>
    {
        public SqlValueSetCodeRepositoryTests(ValueSetCodeRepositoryFixture fixture, ITestOutputHelper output) 
            : base(output)
        {
            this.ValueSetCodeRepository = fixture.ValueSetCodeRepository;
        }

        private IValueSetCodeRepository ValueSetCodeRepository { get; }

        [Theory]
        [Trait(TestTraits.Category, TestCategory.LongRunning)]
        [InlineData("2.16.840.1.113883.3.464.1003.108.12.1011")]
        [InlineData("2.16.840.1.113762.1.4.1045.36")]
        [InlineData("2.16.840.1.113883.3.526.3.1459")]
        [InlineData("2.16.840.1.113883.3.67.1.101.1.278")]
        public void GetValueSetCodes(string valueSetId)
        {
            // Arrange
            // Handled in inline data

            //// Act
            var codes = this.Profiler.ExecuteTimed(() => this.ValueSetCodeRepository.GetValueSetCodes(valueSetId), $"Querying ValueSetId = {valueSetId}");
            this.Output.WriteLine($"Result count: {codes.Count}");

            //// Assert
            Assert.True(codes.Any());
        }
    }
}