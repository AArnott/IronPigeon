// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Providers
{
    using System.Net.Http;
    using IronPigeon.Providers;
    using IronPigeon.Tests;
    using Mocks;
    using Xunit;

    public class TwitterAddressBookTests
    {
        private readonly HttpMessageHandlerRecorder messageRecorder;
        private TwitterAddressBook twitter;

        public TwitterAddressBookTests()
        {
            this.twitter = new TwitterAddressBook();
            this.messageRecorder = Mocks.HttpMessageHandlerRecorder.CreatePlayback(typeof(TwitterAddressBookTests));
            this.twitter.HttpClient = new HttpClient(this.messageRecorder);
        }

        [Fact]
        public void LookupEntryAsyncNonExistentUser()
        {
            this.messageRecorder.SetTestName();
            var endpoint = this.twitter.LookupAsync("@NonExistentUser2394872352").Result;
            Assert.Null(endpoint);
        }

        [Fact]
        public void LookupEntryAsyncValidUserWithNoEntry()
        {
            this.messageRecorder.SetTestName();
            var endpoint = this.twitter.LookupAsync("@shanselman").Result;
            Assert.Null(endpoint);
        }

        [Fact]
        public void LookupEntryAsyncExistingUser()
        {
            this.messageRecorder.SetTestName();
            var endpoint = this.twitter.LookupAsync("@PrivacyNotFound").Result;
            Assert.NotNull(endpoint);
        }

        /// <summary>
        /// Verifies that the #fragment in the URL is verified to match the thumbprint of the downloaded address book entry.
        /// </summary>
        [Fact]
        public void LookupEntryAsyncExistingUserReplacedEndpoint()
        {
            this.messageRecorder.SetTestName();
            Assert.Throws<BadAddressBookEntryException>(() => this.twitter.LookupAsync("@PrivacyNotFound").GetAwaiter().GetResult());
        }
    }
}
