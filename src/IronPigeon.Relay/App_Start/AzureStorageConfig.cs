// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
    using IronPigeon.Providers;
    using IronPigeon.Relay.Controllers;
    using IronPigeon.Relay.Models;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.StorageClient;
    using Validation;

    public class AzureStorageConfig
    {
        /// <summary>
        /// The key to the Azure account configuration information.
        /// </summary>
        internal const string DefaultCloudConfigurationName = "StorageConnectionString";

        internal static readonly TimeSpan PurgeExpiredBlobsInterval = TimeSpan.FromHours(4);

        public static void RegisterConfiguration()
        {
            var storage = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings[DefaultCloudConfigurationName].ConnectionString);
            var initialization = Task.WhenAll(
                BlobController.OneTimeInitializeAsync(storage),
                InboxController.OneTimeInitializeAsync(storage),
                WindowsPushNotificationClientController.OneTimeInitializeAsync(storage),
                AddressBookController.OneTimeInitializeAsync(storage));
            initialization.Wait();
        }
    }
}