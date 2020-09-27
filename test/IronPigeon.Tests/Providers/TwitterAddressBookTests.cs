// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace Providers
{
    using System.Net.Http;
    using IronPigeon;
    using IronPigeon.Providers;
    using Xunit;

    public class TwitterAddressBookTests
    {
        private readonly HttpMessageHandlerRecorder messageRecorder;
        private TwitterAddressBook twitter;

        public TwitterAddressBookTests()
        {
            this.messageRecorder = HttpMessageHandlerRecorder.CreatePlayback(typeof(TwitterAddressBookTests));
#pragma warning disable CA2000 // Dispose objects before losing scope
            this.twitter = new TwitterAddressBook(new HttpClient(this.messageRecorder));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        [Fact]
        public void LookupEntryAsyncNonExistentUser()
        {
            this.messageRecorder.SetTestName();
            Endpoint? endpoint = this.twitter.LookupAsync("@NonExistentUser2394872352").Result;
            Assert.Null(endpoint);
        }

        [Fact]
        public void LookupEntryAsyncValidUserWithNoEntry()
        {
            this.messageRecorder.SetTestName();
            Endpoint? endpoint = this.twitter.LookupAsync("@shanselman").Result;
            Assert.Null(endpoint);
        }

        [Fact(Skip = "Requires new recorded data.")]
        public void LookupEntryAsyncExistingUser()
        {
            this.messageRecorder.SetTestName();
            Endpoint? endpoint = this.twitter.LookupAsync("@PrivacyNotFound").Result;
            Assert.NotNull(endpoint);
        }

        /// <summary>
        /// Verifies that the #fragment in the URL is verified to match the thumbprint of the downloaded address book entry.
        /// </summary>
        [Fact(Skip = "Requires new recorded data.")]
        public void LookupEntryAsyncExistingUserReplacedEndpoint()
        {
            this.messageRecorder.SetTestName();
            Assert.Throws<BadAddressBookEntryException>(() => this.twitter.LookupAsync("@PrivacyNotFound").GetAwaiter().GetResult());
        }
    }
}
