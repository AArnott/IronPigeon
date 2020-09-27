// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Providers;
    using MessagePack;
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
        public const int MaxAddressBookEntrySize = 10 * 1024;
        public static readonly TimeSpan MaxAddressBookEntryLifetime = TimeSpan.FromDays(365 * 20);

        private static readonly SortedDictionary<int, TimeSpan> MaxBlobSizesAndLifetimes = new SortedDictionary<int, TimeSpan>
        {
            { 10 * 1024, TimeSpan.FromDays(30) },
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
            long? lengthLimit;
            if (this.Request.ContentLength.HasValue)
            {
                lengthLimit = this.Request.ContentLength.Value;
                IActionResult? errorResponse = GetDisallowedLifetimeResponse(this.Request.ContentLength.Value, lifetime, this.Request.ContentType);
                if (errorResponse is object)
                {
                    return errorResponse;
                }
            }
            else
            {
                if (!TryGetMaxAllowedBlobSize(lifetime, this.Request.ContentType, out lengthLimit))
                {
                    return this.BadRequest();
                }
            }

            var azureBlobStorage = new AzureBlobStorage(this.azureStorage.PayloadBlobsContainer);
            try
            {
                using var lengthLimitingStream = new StreamWithProgress(this.Request.Body, null) { LengthLimit = lengthLimit };
                Stream? blobStream = lengthLimitingStream;
                if (this.Request.ContentType == AddressBookEntry.ContentType.MediaType)
                {
                    // If they're claiming this is an address book entry (which grants them extra lifetime for the blob),
                    // we want to validate that it is actually what they claim it to be.
                    blobStream = await IsValidAddressBookEntryAsync(lengthLimitingStream, cancellationToken);
                    if (blobStream is null)
                    {
                        return this.BadRequest("The address book entry is invalid.");
                    }
                }

                DateTime expirationUtc = DateTime.UtcNow + lifetime;
                Uri blobUri = await azureBlobStorage.UploadMessageAsync(blobStream, expirationUtc, cancellationToken: cancellationToken);
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

        /// <summary>
        /// Validates that a stream contains a valid <see cref="AddressBookEntry"/>.
        /// </summary>
        /// <param name="incomingStream">The stream that may contain the <see cref="AddressBookEntry"/>. This stream must be verifiably short so we don't allocate unbounded memory.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A copy of the stream that was read.</returns>
        private static async Task<MemoryStream?> IsValidAddressBookEntryAsync(Stream incomingStream, CancellationToken cancellationToken)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            var streamCopy = new MemoryStream(MaxAddressBookEntrySize);
#pragma warning restore CA2000 // Dispose objects before losing scope
            await incomingStream.CopyToAsync(streamCopy, cancellationToken);
            try
            {
                // Deserialization and extraction are both required to ensure that it is fully valid.
                streamCopy.Position = 0;
                AddressBookEntry abe = await MessagePackSerializer.DeserializeAsync<AddressBookEntry>(streamCopy, Utilities.MessagePackSerializerOptions, cancellationToken);
                abe.ExtractEndpoint();
            }
            catch (BadAddressBookEntryException)
            {
                return null;
            }
            catch (MessagePackSerializationException)
            {
                return null;
            }

            streamCopy.Position = 0;
            return streamCopy;
        }

        private static bool TryGetMaxAllowedBlobSize(TimeSpan lifetime, string contentType, [NotNullWhen(true)] out long? maxSize)
        {
            if (contentType == AddressBookEntry.ContentType.MediaType)
            {
                maxSize = MaxAddressBookEntrySize;
                return true;
            }

            IEnumerable<int>? maxBlobSizeAllowingLifetime = from kv in MaxBlobSizesAndLifetimes
                                                            where kv.Value >= lifetime
                                                            select kv.Key;
            if (maxBlobSizeAllowingLifetime.Any())
            {
                maxSize = maxBlobSizeAllowingLifetime.Max();
                return true;
            }

            maxSize = -1;
            return false;
        }

        private static IActionResult? GetDisallowedLifetimeResponse(long blobSize, TimeSpan lifetime, string contentType)
        {
            // We have a special max lifetime for address book entries.
            if (contentType == AddressBookEntry.ContentType.MediaType && lifetime <= MaxAddressBookEntryLifetime)
            {
                return blobSize <= MaxAddressBookEntrySize ? null : (IActionResult)new StatusCodeResult(StatusCodes.Status413RequestEntityTooLarge);
            }

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
