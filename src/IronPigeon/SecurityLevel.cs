// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    /// <summary>
    /// Security level options.
    /// </summary>
    public enum SecurityLevel
    {
        /// <summary>
        /// Minimum security level. Useful for unit testing.
        /// </summary>
        Minimum,

        /// <summary>
        /// It can't get much higher than this while retaining sanity.
        /// </summary>
        Maximum,
    }
}
