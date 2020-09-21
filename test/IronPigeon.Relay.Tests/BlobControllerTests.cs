// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IronPigeon.Providers;
using IronPigeon.Relay;
using IronPigeon.Relay.Tests;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

public class BlobControllerTests : TestBase, IClassFixture<RelayAppFactory>, IAsyncLifetime
{
    private readonly RelayAppFactory factory;
    private readonly HttpClient httpClient;
    private readonly Uri blobPostUrl;

    public BlobControllerTests(RelayAppFactory factory, ITestOutputHelper logger)
        : base(logger)
    {
        factory.Logger = logger;
        this.factory = factory;
        this.httpClient = this.factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        this.blobPostUrl = new Uri("blob", UriKind.Relative);
    }

    public enum LengthHeader
    {
        Absent,
        Accurate,
        Misleading,
    }

    public async Task InitializeAsync()
    {
        await Startup.InitializeDatabasesAsync(this.TimeoutToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LifetimeInMinutes_Omitted()
    {
        using var content = new ByteArrayContent(new byte[1]);
        HttpResponseMessage response = await this.httpClient.PostAsync(this.blobPostUrl, content, this.TimeoutToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory, PairwiseData]
    public Task SmallBlobAcceptedWithLongExpiration_IsAccepted(LengthHeader header) => this.UploadAsync(header, 2 * 1024, TimeSpan.FromDays(5 * 365), HttpStatusCode.Created);

    [Theory, PairwiseData]
    public Task ModerateBlobWithShortExpiration_IsAccepted(LengthHeader header) => this.UploadAsync(header, 30 * 1024, TimeSpan.FromDays(3), HttpStatusCode.Created);

    [Theory, PairwiseData]
    public Task ModerateBlobWithLongExpiration_IsRejected(LengthHeader header) => this.UploadAsync(header, 30 * 1024, TimeSpan.FromDays(5 * 365), HttpStatusCode.PaymentRequired);

    [Theory, PairwiseData]
    public Task LargeBlobWithShortExpiration_IsRejected(LengthHeader header) => this.UploadAsync(header, 5 * 1024 * 1024, TimeSpan.FromDays(1), HttpStatusCode.RequestEntityTooLarge);

    private async Task UploadAsync(LengthHeader lengthHeader, long length, TimeSpan expiration, HttpStatusCode expectedCode)
    {
        using var content = new ByteArrayContent(new byte[length])
        {
            Headers =
            {
                ContentLength = lengthHeader switch
                {
                    LengthHeader.Absent => null,
                    LengthHeader.Accurate => length,
                    LengthHeader.Misleading => 1024,
                    _ => throw new ArgumentOutOfRangeException(nameof(lengthHeader)),
                },
            },
        };
        Uri requestUri = new Uri(this.blobPostUrl.OriginalString + "?lifetimeInMinutes=" + (long)expiration.TotalMinutes, UriKind.Relative);
        try
        {
            HttpResponseMessage response = await this.httpClient.PostAsync(requestUri, content, this.TimeoutToken);
            Assert.Equal(expectedCode, response.StatusCode);
            Assert.NotEqual(LengthHeader.Misleading, lengthHeader);
        }
#pragma warning disable CA1031 // Do not catch general exception types - this is the actual type of exception thrown.
        catch (Exception) when (lengthHeader == LengthHeader.Misleading || (lengthHeader == LengthHeader.Absent && expectedCode != HttpStatusCode.Created))
#pragma warning restore CA1031 // Do not catch general exception types
        {
            // Expected exception
        }
    }
}
