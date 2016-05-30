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

    public abstract class CloudBlobStorageProviderTestBase
    {
        protected ICloudBlobStorageProvider Provider { get; set; }

        [Fact(Skip = "Ignored")]
        public void UploadMessageAsync()
        {
            var uri = this.UploadMessageHelperAsync().Result;
            var client = new HttpClient();
            var downloadedBody = client.GetByteArrayAsync(uri).Result;
            Assert.Equal(Valid.MessageContent, downloadedBody);
        }

        protected async Task<Uri> UploadMessageHelperAsync()
        {
            var body = new MemoryStream(Valid.MessageContent);
            var uri = await this.Provider.UploadMessageAsync(body, Valid.ExpirationUtc);
            return uri;
        }
    }
}
