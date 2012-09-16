namespace IronPigeon.Tests.Providers {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using IronPigeon.Providers;
	using NUnit.Framework;

	[TestFixture]
	public class RelayCloudBlobStorageProviderTests {
		private ICloudBlobStorageProvider provider;
		
		[SetUp]
		public void SetUp() {
			var provider = new RelayCloudBlobStorageProvider(new Uri("http://localhost:39472/api/blob"));
			provider.HttpClient = new HttpClient(Mocks.HttpMessageHandlerRecorder.CreatePlayback());
			this.provider = provider;
		}

		[Test]
		public void UploadTest() {
			var content = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
			var location = this.provider.UploadMessageAsync(
				content, DateTime.UtcNow + TimeSpan.FromMinutes(5.5), "application/testcontent", "testencoding").Result;
			Assert.AreEqual("http://127.0.0.1:10000/devstoreaccount1/blobs/2012.08.26/22A0FLkPHlM-T5q", location.AbsoluteUri);
		}
	}
}
