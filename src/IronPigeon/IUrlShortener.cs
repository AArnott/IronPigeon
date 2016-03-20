// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A service that can shorten long URLs.
    /// </summary>
    public interface IUrlShortener
    {
        /// <summary>
        /// Shortens the specified long URL.
        /// </summary>
        /// <param name="longUrl">The long URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A task whose result is the shortened URL.
        /// </returns>
        Task<Uri> ShortenAsync(Uri longUrl, CancellationToken cancellationToken = default(CancellationToken));
    }
}
