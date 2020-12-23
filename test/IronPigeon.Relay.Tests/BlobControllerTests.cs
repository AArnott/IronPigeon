// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IronPigeon;
using IronPigeon.Relay;
using IronPigeon.Relay.Controllers;
using MessagePack;
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
        await Startup.InitializeDatabasesAsync(this.TimeoutToken, skipTableStorage: true);
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
    public Task SmallBlobAcceptedWithLongExpiration_IsAccepted(LengthHeader header) => this.UploadAsync(header, 2 * 1024, TimeSpan.FromDays(25), HttpStatusCode.Created);

    [Theory, PairwiseData]
    public Task ModerateBlobWithShortExpiration_IsAccepted(LengthHeader header) => this.UploadAsync(header, 30 * 1024, TimeSpan.FromDays(3), HttpStatusCode.Created);

    [Theory, PairwiseData]
    public Task ModerateBlobWithLongExpiration_IsRejected(LengthHeader header) => this.UploadAsync(header, 30 * 1024, TimeSpan.FromDays(5 * 365), HttpStatusCode.PaymentRequired);

    [Theory, PairwiseData]
    public Task LargeBlobWithShortExpiration_IsRejected(LengthHeader header) => this.UploadAsync(header, 5 * 1024 * 1024, TimeSpan.FromDays(1), HttpStatusCode.RequestEntityTooLarge);

    [Fact]
    public async Task UploadAddressBookEntry_Valid()
    {
        OwnEndpoint endpoint = await OwnEndpoint.CreateAsync(CryptoSettings.Testing, new MockEndpointInboxFactory(), this.TimeoutToken);
        var abe = new AddressBookEntry(endpoint);
        byte[] serializedAddressBookEntry = MessagePackSerializer.Serialize(abe, Utilities.MessagePackSerializerOptions, this.TimeoutToken);
        using HttpResponseMessage response = await this.UploadAddressBookHelperAsync(serializedAddressBookEntry);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location.OriginalString);
        this.Logger.WriteLine("Address book entry URL: {0}", response.Headers.Location.OriginalString);
    }

    [Fact]
    public async Task UploadAddressBookEntry_Corrupted()
    {
        OwnEndpoint endpoint = await OwnEndpoint.CreateAsync(CryptoSettings.Testing, new MockEndpointInboxFactory(), this.TimeoutToken);
        var abe = new AddressBookEntry(endpoint);
        byte[] serializedAddressBookEntry = MessagePackSerializer.Serialize(abe, Utilities.MessagePackSerializerOptions, this.TimeoutToken);
        TestUtilities.ApplyFuzzing(serializedAddressBookEntry, 5);
        using HttpResponseMessage response = await this.UploadAddressBookHelperAsync(serializedAddressBookEntry);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadAddressBookEntry_LifetimeTooLarge()
    {
        OwnEndpoint endpoint = await OwnEndpoint.CreateAsync(CryptoSettings.Testing, new MockEndpointInboxFactory(), this.TimeoutToken);
        var abe = new AddressBookEntry(endpoint);
        byte[] serializedAddressBookEntry = MessagePackSerializer.Serialize(abe, Utilities.MessagePackSerializerOptions, this.TimeoutToken);
        using HttpResponseMessage response = await this.UploadAddressBookHelperAsync(serializedAddressBookEntry, lifetimeTooLarge: true);
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }

    [Fact]
    public async Task UploadAddressBookEntry_Invalid()
    {
        using HttpResponseMessage response = await this.UploadAddressBookHelperAsync(new byte[3]);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadAddressBookEntry_TooLargeWithHeader()
    {
        using HttpResponseMessage response = await this.UploadAddressBookHelperAsync(new byte[30 * 1024]);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task UploadAddressBookEntry_TooLargeWithoutHeader()
    {
        await Assert.ThrowsAsync<Exception>(() => this.UploadAddressBookHelperAsync(new byte[30 * 1024], includeContentLengthHeader: false));
    }

    private async Task<HttpResponseMessage> UploadAddressBookHelperAsync(byte[] serializedAddressBookEntry, bool includeContentLengthHeader = true, bool lifetimeTooLarge = false)
    {
        using var content = new ByteArrayContent(serializedAddressBookEntry)
        {
            Headers =
            {
                ContentType = AddressBookEntry.ContentType,
                ContentLength = includeContentLengthHeader ? (long?)serializedAddressBookEntry.Length : null,
            },
        };
        long lifetime = (long)BlobController.MaxAddressBookEntryLifetime.TotalMinutes;
        if (lifetimeTooLarge)
        {
            lifetime++;
        }

        Uri requestUri = new Uri(this.blobPostUrl.OriginalString + "?lifetimeInMinutes=" + lifetime, UriKind.Relative);
        return await this.httpClient.PostAsync(requestUri, content, this.TimeoutToken);
    }

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
