// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using IronPigeon.Functions;
using IronPigeon.Relay;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable IDE0008 // avoid var

public class InboxManagementTests : TestBase
{
    public InboxManagementTests(ITestOutputHelper logger)
        : base(logger)
    {
    }

    [Fact]
    public async Task Mailbox_Get_FailsWithoutAuth()
    {
        // Create the inbox.
        InboxCreationResponse response = await this.CreateMailboxAsync();
        string name = GetMailboxName(response.MessageReceivingEndpoint);

        // Retrieve 0 elements from the inbox, without authentication.
        HttpRequest request = TestFactory.CreateHttpRequest();
        request.Path = response.MessageReceivingEndpoint.AbsolutePath;
        Assert.IsType<UnauthorizedResult>(await Inbox.GetInboxAsync(request, name));
    }

    [Fact]
    public async Task Mailbox_Get_Empty()
    {
        // Create the inbox.
        InboxCreationResponse response = await this.CreateMailboxAsync();
        var (request, name) = PrepareInboxRequest(response);

        // Retrieve 0 elements from the inbox, without authentication.
        request.HttpContext.Response.Body = new ResponseStream();
        Assert.IsType<EmptyResult>(await Inbox.GetInboxAsync(request, name));
        Assert.True(((ResponseStream)request.HttpContext.Response.Body).DisposeAttempted);
        Assert.Equal(StatusCodes.Status200OK, request.HttpContext.Response.StatusCode);
        Assert.Equal(0, request.HttpContext.Response.Body.Length);
    }

    [Fact]
    public async Task Mailbox_Get_NotFound()
    {
        // Create the inbox.
        InboxCreationResponse response = await this.CreateMailboxAsync();
        var (request, name) = PrepareInboxRequest(response);

        // Munge up the mailbox name, and make up some authentication.
        ScrambleInboxName(request, ref name);
        Assert.IsType<NotFoundResult>(await Inbox.GetInboxAsync(request, name));
    }

    [Fact]
    public async Task Mailbox_Delete_EmptyMailbox()
    {
        // Create the inbox.
        InboxCreationResponse response = await this.CreateMailboxAsync();
        var (request, name) = PrepareInboxRequest(response);

        // Delete it.
        Assert.IsType<OkResult>(await Inbox.DeleteInboxAsync(request, name, this.Logger, this.TimeoutToken));

        // Try deleting again.
        Assert.IsType<NotFoundResult>(await Inbox.DeleteInboxAsync(request, name, this.Logger, this.TimeoutToken));
    }

    [Fact]
    public async Task Mailbox_Create_Post_NoContentLength()
    {
        // Create the inbox.
        InboxCreationResponse response = await this.CreateMailboxAsync();
        var (request, name) = PrepareInboxRequest(response);

        request.Body = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = Assert.IsType<StatusCodeResult>(await Inbox.PostInboxAsync(request, name, this.Logger, this.TimeoutToken));
        Assert.Equal(StatusCodes.Status411LengthRequired, result.StatusCode);
    }

    [Fact]
    public async Task Mailbox_Create_Post_NoInbox()
    {
        // Create the inbox.
        InboxCreationResponse response = await this.CreateMailboxAsync();
        var (request, name) = PrepareInboxRequest(response);
        ScrambleInboxName(request, ref name);

        request.Body = new MemoryStream(new byte[] { 1, 2, 3 });
        request.Headers.Add("Content-Length", request.Body.Length.ToString(CultureInfo.InvariantCulture));
        Assert.IsType<NotFoundResult>(await Inbox.PostInboxAsync(request, name, this.Logger, this.TimeoutToken));
    }

    [Fact]
    public async Task Mailbox_Create_Post_NoLifetime()
    {
        // Create the inbox.
        InboxCreationResponse response = await this.CreateMailboxAsync();
        var (request, name) = PrepareInboxRequest(response);

        request.Body = new MemoryStream(new byte[] { 1, 2, 3 });
        request.Headers.Add("Content-Length", request.Body.Length.ToString(CultureInfo.InvariantCulture));
        var result = Assert.IsType<BadRequestErrorMessageResult>(await Inbox.PostInboxAsync(request, name, this.Logger, this.TimeoutToken));
        this.Logger.LogInformation(result.Message);
    }

    [Fact]
    public async Task Mailbox_Create_Post_Retrieve()
    {
        // Create the inbox.
        InboxCreationResponse response = await this.CreateMailboxAsync();
        var (request, name) = PrepareInboxRequest(response);

        request.Body = new MemoryStream(new byte[] { 1, 2, 3 });
        request.QueryString = new QueryString("?lifetime=5");
        request.Headers.Add("Content-Length", request.Body.Length.ToString(CultureInfo.InvariantCulture));
        Assert.IsType<OkResult>(await Inbox.PostInboxAsync(request, name, this.Logger, this.TimeoutToken));

        (request, name) = PrepareInboxRequest(response);
        request.HttpContext.Response.Body = new ResponseStream();
        Assert.IsType<EmptyResult>(await Inbox.GetInboxAsync(request, name));

        // Parse the response stream for the data.
        // TODO: here here to parse the stream.
    }

    private static string GetMailboxName(Uri messageReceivingEndpoint)
    {
        return messageReceivingEndpoint.AbsolutePath.Substring(messageReceivingEndpoint.AbsolutePath.LastIndexOf('/') + 1);
    }

    private static (HttpRequest Request, string Name) PrepareInboxRequest(InboxCreationResponse inbox)
    {
        HttpRequest request = TestFactory.CreateHttpRequest();
        request.Path = inbox.MessageReceivingEndpoint.AbsolutePath;
        request.Headers.Add("Authorization", "Bearer " + inbox.InboxOwnerCode);
        return (request, GetMailboxName(inbox.MessageReceivingEndpoint));
    }

    private static void ScrambleInboxName(HttpRequest request, ref string name)
    {
        request.Path = request.Path.Value.Replace(name, "DOESNOTEXIST");
        name = "DOESNOTEXIST";
    }

    private async Task<InboxCreationResponse> CreateMailboxAsync()
    {
        HttpRequest request = TestFactory.CreateHttpRequest();
        var result = (OkObjectResult)await IronPigeon.Functions.CreateInbox.CreateInboxAsync(request, this.Logger);
        var response = (InboxCreationResponse)result.Value;
        Assert.NotNull(response.InboxOwnerCode);
        Assert.NotNull(response.MessageReceivingEndpoint);
        return response;
    }

    private class ResponseStream : MemoryStream
    {
        internal bool DisposeAttempted { get; private set; }

#pragma warning disable CA2215 // Dispose methods should call base class dispose
        protected override void Dispose(bool disposing)
#pragma warning restore CA2215 // Dispose methods should call base class dispose
        {
            // Do NOT allow the stream to be disposed so we can read from it in our tests.
            this.DisposeAttempted = true;
        }
    }
}
