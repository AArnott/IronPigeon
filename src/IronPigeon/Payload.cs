// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using Validation;

    /// <summary>
    /// The payload in a securely transmitted message, before encryptions and signatures are applied
    /// or after they are removed.
    /// </summary>
    [DataContract]
    public class Payload : IEquatable<Payload>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Payload" /> class.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <param name="contentType">Type of the content.</param>
        public Payload(byte[] content, string contentType)
        {
            Requires.NotNull(content, "content");
            Requires.NotNullOrEmpty(contentType, "contentType");

            this.Content = content;
            this.ContentType = contentType;
        }

        /// <summary>
        /// Gets or sets the blob that constitutes the payload.
        /// </summary>
        [DataMember]
        public byte[] Content { get; set; }

        /// <summary>
        /// Gets or sets the content-type that describes the type of data that is
        /// serialized in the <see cref="Content"/> property.
        /// </summary>
        [DataMember]
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the location of the payload reference that led to the discovery of this payload.
        /// </summary>
        internal Uri PayloadReferenceUri { get; set; }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(Payload other)
        {
            if (other == null)
            {
                return false;
            }

            if (this.ContentType != other.ContentType)
            {
                return false;
            }

            if (this.Content == other.Content)
            {
                return true;
            }

            if (this.Content == null || other.Content == null)
            {
                return false;
            }

            if (this.Content.Length != other.Content.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Content.Length; i++)
            {
                if (this.Content[i] != other.Content[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
