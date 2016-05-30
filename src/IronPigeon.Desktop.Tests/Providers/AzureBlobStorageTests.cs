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
    using IronPigeon.Providers;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Xunit;

    public class AzureBlobStorageTests : CloudBlobStorageProviderTestBase, IDisposable
    {
        private string testContainerName;
        private CloudBlobContainer container;
        private AzureBlobStorage provider;

        public AzureBlobStorageTests()
        {
            this.testContainerName = "unittests" + Guid.NewGuid().ToString();
            var account = CloudStorageAccount.DevelopmentStorageAccount;
            var blobClient = account.CreateCloudBlobClient();
            this.Provider = this.provider = new AzureBlobStorage(account, this.testContainerName);
            this.provider.CreateContainerIfNotExistAsync().GetAwaiter().GetResult();
            this.container = blobClient.GetContainerReference(this.testContainerName);
        }

        public void Dispose()
        {
            if (this.container != null)
            {
                this.container.Delete();
            }
        }

        [Fact(Skip = "Ignored")]
        public void CreateWithContainerAsync()
        {
            // The SetUp method already called the method, so this tests the results of it.
            var permissions = this.container.GetPermissions();
            Assert.Equal(BlobContainerPublicAccessType.Blob, permissions.PublicAccess);
        }

        [Fact(Skip = "Ignored")]
        public void PurgeBlobsExpiringBeforeAsync()
        {
            this.UploadMessageHelperAsync().GetAwaiter().GetResult();
            this.provider.PurgeBlobsExpiringBeforeAsync(DateTime.UtcNow.AddDays(7)).GetAwaiter().GetResult();
            Assert.Equal(0, this.container.ListBlobs().Count());
        }
    }
}
