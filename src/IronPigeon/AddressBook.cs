// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Retrieves contacts from some public store.
    /// </summary>
    /// <remarks>
    /// This class does not describe a method for publishing to an address book because
    /// each address book may have different authentication requirements.
    /// Derived types are expected to be thread-safe.
    /// </remarks>
    public abstract class AddressBook
    {
        /// <summary>
        /// Retrieves a contact with some user supplied identifier.
        /// </summary>
        /// <param name="identifier">The user-supplied identifier for the contact.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>
        /// A task whose result is the contact, or null if no match is found.
        /// </returns>
        /// <exception cref="BadAddressBookEntryException">Thrown when a validation error occurs while reading the address book entry.</exception>
        public abstract Task<Endpoint?> LookupAsync(string identifier, CancellationToken cancellationToken = default);
    }
}
