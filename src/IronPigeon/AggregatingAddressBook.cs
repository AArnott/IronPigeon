// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft;

    /// <summary>
    /// A caching address book that is made up of other address books.
    /// </summary>
    public class AggregatingAddressBook
    {
        /// <summary>
        /// A cache of identifiers and their resolved endpoints.
        /// </summary>
        private readonly ConcurrentDictionary<string, Endpoint> resolvedIdentifiersCache = new ConcurrentDictionary<string, Endpoint>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregatingAddressBook"/> class.
        /// </summary>
        /// <param name="addressBooks">The address books that may be consulted to resolve identifiers to <see cref="Endpoint"/> instances.</param>
        public AggregatingAddressBook(IReadOnlyList<AddressBook> addressBooks)
        {
            this.AddressBooks = addressBooks ?? throw new ArgumentNullException(nameof(addressBooks));
        }

        /// <summary>
        /// Gets the address books that may be consulted to resolve identifiers to <see cref="Endpoint"/> instances.
        /// </summary>
        public IReadOnlyList<AddressBook> AddressBooks { get; }

        /// <summary>
        /// Gets the set of identifiers this endpoint claims that are verifiable.
        /// </summary>
        /// <param name="endpoint">The endpoint whose authorized identifiers are to be verified.</param>
        /// <param name="identifiers">The identifiers to check.</param>
        /// <param name="cancellationToken">A general cancellation token on the request.</param>
        /// <returns>A task whose result is the set of verified identifiers.</returns>
        public async Task<IReadOnlyCollection<string>> GetVerifiableIdentifiersAsync(Endpoint endpoint, IEnumerable<string> identifiers, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(endpoint, nameof(endpoint));
            Requires.NotNull(identifiers, nameof(identifiers));

            var verifiedIdentifiers = new List<string>();
            var map = identifiers.Where(id => id is object).ToDictionary(
                id => id,
                id => this.IsVerifiableIdentifierAsync(endpoint, id, cancellationToken));
            await Task.WhenAll(map.Values).ConfigureAwait(false);
            foreach (KeyValuePair<string, Task<bool>> result in map)
            {
                if (await result.Value.ConfigureAwait(false))
                {
                    verifiedIdentifiers.Add(result.Key);
                }
            }

            return verifiedIdentifiers;
        }

        /// <summary>
        /// Checks whether the specified identifier yields an endpoint equivalent to this one.
        /// </summary>
        /// <param name="claimingEndpoint">The endpoint that claims to be resolvable from a given identifier.</param>
        /// <param name="claimedIdentifier">The identifier to check.</param>
        /// <param name="cancellationToken">A general cancellation token on the request.</param>
        /// <returns>A task whose result is <c>true</c> if the identifier verified correctly; otherwise <c>false</c>.</returns>
        private async Task<bool> IsVerifiableIdentifierAsync(Endpoint claimingEndpoint, string claimedIdentifier, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(claimingEndpoint, nameof(claimingEndpoint));
            Requires.NotNullOrEmpty(claimedIdentifier, nameof(claimedIdentifier));

            Endpoint cachedEndpoint;
            if (this.resolvedIdentifiersCache.TryGetValue(claimedIdentifier, out cachedEndpoint))
            {
                return cachedEndpoint.Equals(claimingEndpoint);
            }

            Endpoint? matchingEndpoint = await Utilities.FastestQualifyingResultAsync(
                this.AddressBooks,
                (ct, addressBook) => addressBook.LookupAsync(claimedIdentifier, ct),
                resolvedEndpoint => claimingEndpoint.Equals(resolvedEndpoint),
                cancellationToken).ConfigureAwait(false);

            if (matchingEndpoint is object)
            {
                this.resolvedIdentifiersCache.TryAdd(claimedIdentifier, matchingEndpoint);
                return true;
            }

            return false;
        }
    }
}
