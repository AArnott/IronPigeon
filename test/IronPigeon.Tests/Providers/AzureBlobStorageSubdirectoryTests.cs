// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace Providers
{
    using IronPigeon.Providers;
    using Xunit;
    using Xunit.Abstractions;

    public class AzureBlobStorageSubdirectoryTests : AzureBlobStorageTests
    {
        public AzureBlobStorageSubdirectoryTests(ITestOutputHelper logger)
            : base(logger, "subdir")
        {
        }

        [Fact]
        public void TrailingPathDelimiterTrimmed()
        {
            var provider = new AzureBlobStorage(this.container, "a/");
            Assert.Equal("a", provider.Directory);
        }
    }
}
