// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using IronPigeon;
using Xunit;

public class PayloadReferenceTests
{
    [Fact]
    public void CtorInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>("location", () => new PayloadReference(null!, Valid.ContentType, Valid.Hash, Valid.HashAlgorithmName, Valid.SymmetricEncryptionInputs, Valid.ExpirationUtc));
        Assert.Throws<ArgumentException>("hash", () => new PayloadReference(Valid.Location, Valid.ContentType, null!, Valid.HashAlgorithmName, Valid.SymmetricEncryptionInputs, Valid.ExpirationUtc));
        Assert.Throws<ArgumentNullException>(() => new PayloadReference(Valid.Location, Valid.ContentType, Valid.Hash, null!, Valid.SymmetricEncryptionInputs, Valid.ExpirationUtc));
        Assert.Throws<ArgumentException>(() => new PayloadReference(Valid.Location, Valid.ContentType, Valid.Hash, string.Empty, Valid.SymmetricEncryptionInputs, Valid.ExpirationUtc));
        Assert.Throws<ArgumentNullException>(() => new PayloadReference(Valid.Location, Valid.ContentType, Valid.Hash, Valid.HashAlgorithmName, null!, Valid.ExpirationUtc));
        Assert.Throws<ArgumentException>(() => new PayloadReference(Valid.Location, Valid.ContentType, Valid.Hash, Valid.HashAlgorithmName, Valid.SymmetricEncryptionInputs, Invalid.ExpirationUtc));
        Assert.Throws<ArgumentException>(() => new PayloadReference(Valid.Location, Valid.ContentType, Invalid.Hash, Valid.HashAlgorithmName, Valid.SymmetricEncryptionInputs, Valid.ExpirationUtc));
    }

    [Fact]
    public void Ctor()
    {
        var reference = new PayloadReference(Valid.Location, Valid.ContentType, Valid.Hash, Valid.HashAlgorithmName, Valid.SymmetricEncryptionInputs, Valid.ExpirationUtc);
        Assert.Same(Valid.Location, reference.Location);
        Assert.Same(Valid.ContentType, reference.ContentType);
        Assert.Equal(Valid.HashAlgorithmName, reference.HashAlgorithmName);
        Assert.Equal(Valid.Hash, reference.Hash);
        Assert.Same(Valid.SymmetricEncryptionInputs, reference.DecryptionInputs);
        Assert.Equal(Valid.ExpirationUtc, reference.ExpiresUtc);
    }
}
