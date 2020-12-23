// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// A tuple containing an endpoint's private data and a location where the public endpoint can be downloaded.
    /// </summary>
    [DataContract]
    public class EndpointAndAddressBookUri
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EndpointAndAddressBookUri"/> class.
        /// </summary>
        /// <param name="addressBookUri">The URI where the address book entry is stored.</param>
        /// <param name="endpoint">The user's own endpoint.</param>
        public EndpointAndAddressBookUri(Uri addressBookUri, OwnEndpoint endpoint)
        {
            this.AddressBookUri = addressBookUri;
            this.Endpoint = endpoint;
        }

        /// <summary>
        /// Gets the URI where the address book entry is stored.
        /// </summary>
        [DataMember]
        public Uri AddressBookUri { get; }

        /// <summary>
        /// Gets the user's own endpoint.
        /// </summary>
        [DataMember]
        public OwnEndpoint Endpoint { get; }
    }
}
