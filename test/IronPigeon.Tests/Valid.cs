// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Mime;
using IronPigeon;

internal static class Valid
{
    internal const string ContactIdentifier = "some identifier";
    internal const string HashAlgorithmName = "SHA1";
    internal static readonly ContentType ContentType = new ContentType("application/ironpigeon");
    internal static readonly byte[] Hash = new byte[1];
    internal static readonly byte[] Key = new byte[1];
    internal static readonly byte[] IV = new byte[1];
    internal static readonly SymmetricEncryptionInputs SymmetricEncryptionInputs = new SymmetricEncryptionInputs(PCLCrypto.SymmetricAlgorithm.AesCbcPkcs7, Key, IV);

    internal static readonly Uri Location = new Uri("http://localhost/");
    internal static readonly DateTime ExpirationUtc = DateTime.UtcNow.AddDays(1);
    internal static readonly byte[] MessageContent = new byte[] { 0x11, 0x22, 0x33 };

    internal static readonly Uri SampleMessageReceivingEndpoint = new Uri("http://localhost/inbox/someone");
}
