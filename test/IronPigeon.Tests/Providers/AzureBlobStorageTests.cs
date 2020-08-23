// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using IronPigeon.Providers;
    using Xunit;

    public class AzureBlobStorageTests : CloudBlobStorageProviderTestBase, IAsyncLifetime
    {
        private BlobContainerClient container;
        private AzureBlobStorage provider;

        public AzureBlobStorageTests()
        {
            string testContainerName = "unittests" + Guid.NewGuid().ToString();
            this.container = new BlobContainerClient("UseDevelopmentStorage=true", testContainerName);
            this.Provider = this.provider = new AzureBlobStorage(this.container);
        }

        public async Task InitializeAsync()
        {
            await this.provider.CreateContainerIfNotExistAsync(this.TimeoutToken);
        }

        public async Task DisposeAsync()
        {
            await this.container.DeleteAsync(cancellationToken: this.TimeoutToken);
        }

        [Fact]
        public async Task CreateContainerIfNotExistAsync_SetsPublicAccessPolicy()
        {
            // InitializeAsync has already created the container.
            Azure.Response<BlobContainerAccessPolicy>? accessPolicy = await this.container.GetAccessPolicyAsync(cancellationToken: this.TimeoutToken);
            Assert.Equal(PublicAccessType.Blob, accessPolicy.Value.BlobPublicAccess);
        }

        [Fact]
        public async Task PurgeBlobsExpiringBeforeAsync()
        {
            await this.UploadMessageHelperAsync();
            await this.provider.PurgeBlobsExpiringBeforeAsync(DateTime.UtcNow.AddDays(7));
            await foreach (BlobItem? blob in this.container.GetBlobsAsync(cancellationToken: this.TimeoutToken))
            {
                Assert.False(true, "Container not empty.");
            }
        }
    }
}
