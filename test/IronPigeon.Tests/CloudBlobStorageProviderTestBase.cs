// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public abstract class CloudBlobStorageProviderTestBase : TestBase
    {
        protected CloudBlobStorageProviderTestBase(ITestOutputHelper logger)
            : base(logger)
        {
        }

        protected ICloudBlobStorageProvider? Provider { get; set; }

        [Fact]
        public async Task UploadMessageAsync()
        {
            Uri uri = await this.UploadMessageHelperAsync();
            using var client = new HttpClient();
            var downloadedBody = await client.GetByteArrayAsync(uri);
            Assert.Equal(Valid.MessageContent, downloadedBody);
        }

        protected async Task<Uri> UploadMessageHelperAsync()
        {
            using var body = new MemoryStream(Valid.MessageContent);
            Uri uri = await this.Provider.UploadMessageAsync(body, Valid.ExpirationUtc);
            this.Logger.WriteLine($"Blob uploaded to: {uri.AbsoluteUri}");
            return uri;
        }
    }
}
