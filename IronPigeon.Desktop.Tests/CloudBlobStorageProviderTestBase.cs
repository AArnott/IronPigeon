namespace IronPigeon.Tests {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public abstract class CloudBlobStorageProviderTestBase {
		protected ICloudBlobStorageProvider Provider { get; set; }

		[Test]
		public void UploadMessageAsync() {
			var body = new MemoryStream(Valid.MessageContent);
			var uri = this.Provider.UploadMessageAsync(body, Valid.ExpirationUtc).Result;
			var client = new HttpClient();
			var downloadedBody = client.GetByteArrayAsync(uri).Result;
			Assert.That(downloadedBody, Is.EqualTo(Valid.MessageContent));
		}
	}
}
