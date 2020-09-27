// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.IO;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A service that can upload arbitrary data streams to cloud storage
    /// such that it is retrievable with just an HTTP GET request directed at a <see cref="Uri"/> (without additional headers).
    /// </summary>
    public interface ICloudBlobStorageProvider
    {
        /// <summary>
        /// Uploads a blob to public cloud storage.
        /// </summary>
        /// <param name="content">The blob's content.</param>
        /// <param name="expirationUtc">The date after which this blob may be deleted.</param>
        /// <param name="contentType">An optional content type for the blob. Often null for encrypted blobs, but may be <see cref="AddressBookEntry.ContentType"/> for address book entries.</param>
        /// <param name="bytesCopiedProgress">Receives progress feedback in terms of bytes uploaded.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task whose result is the URL by which the blob's content may be accessed.</returns>
        Task<Uri> UploadMessageAsync(Stream content, DateTime expirationUtc, MediaTypeHeaderValue? contentType = null, IProgress<long>? bytesCopiedProgress = null, CancellationToken cancellationToken = default);
    }
}
