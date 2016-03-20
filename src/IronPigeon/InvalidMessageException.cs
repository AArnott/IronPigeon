// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;

    /// <summary>
    /// An exception thrown when an error occurs while reading a message.
    /// </summary>
    public class InvalidMessageException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidMessageException" /> class.
        /// </summary>
        public InvalidMessageException()
            : this(Strings.InvalidMessage)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidMessageException" /> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public InvalidMessageException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidMessageException" /> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="inner">The inner exception.</param>
        public InvalidMessageException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
