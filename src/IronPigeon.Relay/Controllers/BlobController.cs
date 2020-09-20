// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Providers;
    using Microsoft;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    [ApiController]
    [Route("[controller]")]
    [DenyHttp]
    public class BlobController : ControllerBase
    {
        private static readonly SortedDictionary<int, TimeSpan> MaxBlobSizesAndLifetimes = new SortedDictionary<int, TimeSpan>
        {
            { 10 * 1024, TimeSpan.FromDays(365 * 20) }, // this is intended for address book entries.
            { 512 * 1024, TimeSpan.FromDays(7) },
        };

        private readonly AzureStorage azureStorage;
        private readonly ILogger<BlobController> logger;

        public BlobController(AzureStorage azureStorage, ILogger<BlobController> logger)
        {
            this.azureStorage = azureStorage;
            this.logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> PostAsync([FromQuery] long lifetimeInMinutes, CancellationToken cancellationToken)
        {
            if (lifetimeInMinutes <= 0)
            {
                return this.BadRequest("lifetimeInMinutes is required to be a positive integer in the query string.");
            }

            var lifetime = TimeSpan.FromMinutes(lifetimeInMinutes);
            long lengthLimit;
            if (this.Request.ContentLength.HasValue)
            {
                lengthLimit = this.Request.ContentLength.Value;
                IActionResult? errorResponse = GetDisallowedLifetimeResponse(this.Request.ContentLength.Value, lifetime);
                if (errorResponse is object)
                {
                    return errorResponse;
                }
            }
            else
            {
                lengthLimit = GetMaxAllowedBlobSize(lifetime);
            }

            var azureBlobStorage = new AzureBlobStorage(this.azureStorage.PayloadBlobsContainer);
            try
            {
                using var lengthLimitingStream = new StreamWithProgress(this.Request.Body, null) { LengthLimit = lengthLimit };
                DateTime expirationUtc = DateTime.UtcNow + lifetime;
                Uri blobUri = await azureBlobStorage.UploadMessageAsync(lengthLimitingStream, expirationUtc, cancellationToken: cancellationToken);
                return this.Created(blobUri, blobUri);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
            {
                // They caught us uninitialized. Ask them to try again after we mitigate the problem.
                this.logger.LogError("Request failed because blob container did not exist. Creating it now...");
                await azureBlobStorage.CreateContainerIfNotExistAsync(cancellationToken);
                return this.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (StreamWithProgress.StreamTooLongException)
            {
                // The client is pushing too much data. Cut it off now.
                this.HttpContext.Abort();

                // Return this so the C# compiler is satisfied, but it will likely not be transmitted.
                return this.StatusCode(StatusCodes.Status413RequestEntityTooLarge);
            }
        }

        private static long GetMaxAllowedBlobSize(TimeSpan lifetime) => MaxBlobSizesAndLifetimes.Where(kv => kv.Value >= lifetime).Max(kv => kv.Key);

        private static IActionResult? GetDisallowedLifetimeResponse(long blobSize, TimeSpan lifetime)
        {
            foreach (KeyValuePair<int, TimeSpan> rule in MaxBlobSizesAndLifetimes)
            {
                if (blobSize < rule.Key)
                {
                    if (lifetime > rule.Value)
                    {
                        return new StatusCodeResult(StatusCodes.Status402PaymentRequired);
                    }

                    return null;
                }
            }

            return new StatusCodeResult(StatusCodes.Status413RequestEntityTooLarge);
        }
    }
}
