// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    /// <summary>
    /// An interface that receives log messages.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Receives a message and a buffer.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="buffer">The buffer.</param>
        void WriteLine(string message, byte[]? buffer);
    }
}
