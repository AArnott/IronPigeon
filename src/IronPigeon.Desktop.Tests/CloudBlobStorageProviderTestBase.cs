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

		[Test, Ignore]
		public void UploadMessageAsync() {
			var uri = this.UploadMessageHelperAsync().Result;
			var client = new HttpClient();
			var downloadedBody = client.GetByteArrayAsync(uri).Result;
			Assert.That(downloadedBody, Is.EqualTo(Valid.MessageContent));
		}

		protected async Task<Uri> UploadMessageHelperAsync() {
			var body = new MemoryStream(Valid.MessageContent);
			var uri = await this.Provider.UploadMessageAsync(body, Valid.ExpirationUtc);
			return uri;
		}
	}
}
