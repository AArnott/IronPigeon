// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests.Providers
{
    using System;
    using System.Net.Http;
    using IronPigeon.Providers;
    using IronPigeon.Tests.Mocks;
    using Xunit;

    public class GoogleUrlShortenerTests
    {
        private readonly HttpMessageHandlerRecorder messageRecorder;
        private IUrlShortener shortener;

        public GoogleUrlShortenerTests()
        {
            var shortener = new GoogleUrlShortener();
            this.shortener = shortener;
            this.messageRecorder = HttpMessageHandlerRecorder.CreatePlayback(typeof(GoogleUrlShortenerTests));
            shortener.HttpClient = new HttpClient(this.messageRecorder);
        }

        [Fact]
        public void ShortenAsyncNull()
        {
            Assert.Throws<ArgumentNullException>(() => this.shortener.ShortenAsync(null!).GetAwaiter().GetResult());
        }

        [Fact]
        public void ShortenAsync()
        {
            this.messageRecorder.SetTestName();
            Uri shortUrl = this.shortener.ShortenAsync(new Uri("http://www.google.com/")).GetAwaiter().GetResult();
            Assert.Equal("http://goo.gl/fbsS", shortUrl.AbsoluteUri);
        }

        [Fact]
        public void ShortenExcludeFragmentAsync()
        {
            this.messageRecorder.SetTestName();
            Uri? shortUrl =
                this.shortener.ShortenExcludeFragmentAsync(new Uri("http://www.google.com/#hashtest")).GetAwaiter().GetResult();
            Assert.Equal("http://goo.gl/fbsS#hashtest", shortUrl.AbsoluteUri);
        }

        [Fact]
        public void ShortenExcludeFragmentAsyncNoFragment()
        {
            this.messageRecorder.SetTestName();
            Uri? shortUrl =
                this.shortener.ShortenExcludeFragmentAsync(new Uri("http://www.google.com/")).GetAwaiter().GetResult();
            Assert.Equal("http://goo.gl/fbsS", shortUrl.AbsoluteUri);
        }
    }
}
