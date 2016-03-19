// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Providers
{
    using System.Net.Http;
    using IronPigeon.Providers;
    using IronPigeon.Tests;
    using NUnit.Framework;

    [TestFixture]
    public class TwitterAddressBookTests
    {
        private TwitterAddressBook twitter;

        [SetUp]
        public void SetUp()
        {
            this.twitter = new TwitterAddressBook();
            this.twitter.HttpClient = new HttpClient(Mocks.HttpMessageHandlerRecorder.CreatePlayback());
        }

        [Test]
        public void LookupEntryAsyncNonExistentUser()
        {
            var endpoint = this.twitter.LookupAsync("@NonExistentUser2394872352").Result;
            Assert.That(endpoint, Is.Null);
        }

        [Test]
        public void LookupEntryAsyncValidUserWithNoEntry()
        {
            var endpoint = this.twitter.LookupAsync("@shanselman").Result;
            Assert.That(endpoint, Is.Null);
        }

        [Test]
        public void LookupEntryAsyncExistingUser()
        {
            var endpoint = this.twitter.LookupAsync("@PrivacyNotFound").Result;
            Assert.That(endpoint, Is.Not.Null);
        }

        /// <summary>
        /// Verifies that the #fragment in the URL is verified to match the thumbprint of the downloaded address book entry.
        /// </summary>
        [Test]
        public void LookupEntryAsyncExistingUserReplacedEndpoint()
        {
            Assert.Throws<BadAddressBookEntryException>(() => this.twitter.LookupAsync("@PrivacyNotFound").GetAwaiter().GetResult());
        }
    }
}
