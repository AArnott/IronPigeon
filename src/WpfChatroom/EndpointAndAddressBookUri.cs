// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Runtime.Serialization;
    using IronPigeon;

    [DataContract]
    public class EndpointAndAddressBookUri
    {
        public EndpointAndAddressBookUri(Uri addressBookUri, OwnEndpoint endpoint)
        {
            this.AddressBookUri = addressBookUri;
            this.Endpoint = endpoint;
        }

        [DataMember]
        public Uri AddressBookUri { get; }

        [DataMember]
        public OwnEndpoint Endpoint { get; }
    }
}
